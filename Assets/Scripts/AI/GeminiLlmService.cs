using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

public class GeminiLlmService : MonoBehaviour
{
    private const string ApiKeyEnvironmentVariable = "GEMINI_API_KEY";

    [Header("Authentication")]
    [SerializeField] private string apiKeyOverride;

    [Header("Gemini")]
    [SerializeField] private string modelName = "gemini-2.5-flash";
    [SerializeField] [Min(1f)] private float requestTimeoutSeconds = 20f;
    [SerializeField] [Range(0f, 1f)] private float temperature = 0.1f;

    [Header("Rate Limit Guardrails")]
    [SerializeField] [Min(0f)] private float minSecondsBetweenUserRequests = 0.75f;
    [SerializeField] [Range(0, 3)] private int maxRetriesOnRateLimit = 1;
    [SerializeField] [Min(0.1f)] private float rateLimitBackoffBaseSeconds = 1.25f;

    [Header("Response Guardrails")]
    [SerializeField] private bool logRawModelResponse;

    [Header("Runtime Context")]
    [SerializeField] private MockResponseProcedureRunner procedureRunner;
    [SerializeField] private ProcedureRepository procedureRepository;
    [SerializeField] private CockpitInputRegistry inputRegistry;
    [SerializeField] private bool allowMultipleSimpleAnswerHighlights = true;

    [Header("Alias Overrides")]
    [SerializeField] private List<ControlAliasOverride> aliasOverrides = new List<ControlAliasOverride>();

    private readonly Stage1GeminiApi stage1Api = new Stage1GeminiApi();
    private readonly Stage2GeminiApi stage2Api = new Stage2GeminiApi();

    private bool requestInFlight;
    private float lastRequestStartTime = -100f;

    private void Reset()
    {
        if (procedureRunner == null)
        {
            procedureRunner = FindFirstObjectByType<MockResponseProcedureRunner>();
        }

        if (procedureRepository == null)
        {
            procedureRepository = FindFirstObjectByType<ProcedureRepository>();
        }
    }

    public void RequestResponse(string userCommand, Action<MockLlmResponse> onSuccess, Action<string> onError)
    {
        StartCoroutine(RequestResponseRoutine(userCommand, onSuccess, onError));
    }

    private IEnumerator RequestResponseRoutine(
        string userCommand,
        Action<MockLlmResponse> onSuccess,
        Action<string> onError)
    {
        Debug.Log($"[GeminiLlmService] RequestResponse start. rawCommand='{userCommand}'", this);

        if (string.IsNullOrWhiteSpace(userCommand))
        {
            onError?.Invoke("Command text is empty.");
            yield break;
        }

        string apiKey = ResolveApiKey();
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            onError?.Invoke($"Missing API key. Set {ApiKeyEnvironmentVariable} or apiKeyOverride in inspector.");
            yield break;
        }

        if (requestInFlight)
        {
            onError?.Invoke("Another LLM request is already running.");
            yield break;
        }

        float elapsed = Time.unscaledTime - lastRequestStartTime;
        if (elapsed < minSecondsBetweenUserRequests)
        {
            float wait = minSecondsBetweenUserRequests - elapsed;
            onError?.Invoke($"Rate guard active. Retry in {wait:0.0}s.");
            yield break;
        }

        requestInFlight = true;
        lastRequestStartTime = Time.unscaledTime;

        try
        {
            string command = userCommand.Trim();
            GeminiApiRequestSettings requestSettings = BuildRequestSettings();
            List<string> availableProcedureIds = GetAllowedProcedureIds(out string procedureIdsError);
            if (!string.IsNullOrWhiteSpace(procedureIdsError))
            {
                onError?.Invoke(procedureIdsError);
                yield break;
            }

            Stage1IntentResult stage1Intent = null;
            string stage1Error = null;
            Stage1RequestContext stage1Context = BuildStage1Context(command, availableProcedureIds);

            yield return stage1Api.RequestIntent(
                apiKey,
                requestSettings,
                stage1Context,
                result =>
                {
                    stage1Intent = result;
                    stage1Error = null;
                },
                err =>
                {
                    stage1Intent = null;
                    stage1Error = err;
                });

            if (!string.IsNullOrWhiteSpace(stage1Error))
            {
                Debug.LogError($"[GeminiLlmService] Stage 1 failed: {stage1Error}", this);
                onError?.Invoke(stage1Error);
                yield break;
            }

            Debug.Log($"[GeminiLlmService] Stage 1 success. type='{stage1Intent.responseType}', procedureId='{stage1Intent.procedureId}', resume={stage1Intent.resumeFromStepIndex}", this);

            MockLlmResponse finalResponse = null;
            string finalError = null;

            if (string.Equals(stage1Intent.responseType, "error", StringComparison.OrdinalIgnoreCase))
            {
                finalResponse = Stage2GeminiApi.BuildErrorResponse(stage1Intent.textResponse);
            }
            else
            {
                Stage2RequestContext stage2Context = BuildStage2Context(command, stage1Intent, availableProcedureIds);
                yield return stage2Api.RequestPayload(
                    apiKey,
                    requestSettings,
                    stage2Context,
                    response =>
                    {
                        finalResponse = response;
                        finalError = null;
                    },
                    err =>
                    {
                        finalResponse = null;
                        finalError = err;
                    });
            }

            if (!string.IsNullOrWhiteSpace(finalError))
            {
                Debug.LogError($"[GeminiLlmService] Stage 2 failed: {finalError}", this);
                onError?.Invoke(finalError);
                yield break;
            }

            if (finalResponse == null)
            {
                onError?.Invoke("Final response is null.");
                yield break;
            }

            if (!MockLlmResponseValidator.TryValidateSingleResponse(finalResponse, out string schemaError))
            {
                Debug.LogError($"[GeminiLlmService] Final schema validation failed: {schemaError}", this);
                onError?.Invoke($"Final response validation failed: {schemaError}");
                yield break;
            }

            finalResponse.requestId = Guid.NewGuid().ToString("N");
            finalResponse.timestampMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

            string highlightsDebug = finalResponse.highlightControlIds == null || finalResponse.highlightControlIds.Count == 0
                ? "[]"
                : $"[{string.Join(", ", finalResponse.highlightControlIds)}]";
            Debug.Log($"[GeminiLlmService] Final response accepted. type='{finalResponse.responseType}', procedureId='{finalResponse.procedureId}', resume={finalResponse.resumeFromStepIndex}, highlights={highlightsDebug}", this);

            onSuccess?.Invoke(finalResponse);
        }
        finally
        {
            requestInFlight = false;
        }
    }

    private GeminiApiRequestSettings BuildRequestSettings()
    {
        return new GeminiApiRequestSettings
        {
            modelName = modelName,
            requestTimeoutSeconds = requestTimeoutSeconds,
            temperature = temperature,
            maxRetriesOnRateLimit = maxRetriesOnRateLimit,
            rateLimitBackoffBaseSeconds = rateLimitBackoffBaseSeconds,
            logRawModelResponse = logRawModelResponse
        };
    }

    private Stage1RequestContext BuildStage1Context(string userCommand)
    {
        return new Stage1RequestContext
        {
            userCommand = userCommand,
            availableProcedureIds = new List<string>(),
            activeProcedureId = procedureRunner != null ? procedureRunner.ActiveProcedureId : string.Empty,
            activeStepIndex = procedureRunner != null ? procedureRunner.ActiveStepIndex : 0,
            isProcedureRunning = procedureRunner != null && procedureRunner.IsProcedureRunning,
            isProcedurePaused = procedureRunner != null && procedureRunner.IsProcedurePaused,
            isWaitingForClick = procedureRunner != null && procedureRunner.IsWaitingForClick
        };
    }

    private Stage1RequestContext BuildStage1Context(string userCommand, List<string> availableProcedureIds)
    {
        Stage1RequestContext context = BuildStage1Context(userCommand);
        context.availableProcedureIds = availableProcedureIds == null
            ? new List<string>()
            : new List<string>(availableProcedureIds);
        return context;
    }

    private Stage2RequestContext BuildStage2Context(string userCommand, Stage1IntentResult stage1Intent, List<string> availableProcedureIds)
    {
        List<string> controlIds = GetAvailableControlIds();
        return new Stage2RequestContext
        {
            userCommand = userCommand,
            stage1Intent = stage1Intent,
            availableProcedureIds = availableProcedureIds == null
                ? new List<string>()
                : new List<string>(availableProcedureIds),
            availableControlIds = controlIds,
            controlAliases = BuildControlAliasMap(controlIds),
            allowMultipleHighlights = allowMultipleSimpleAnswerHighlights
        };
    }

    private List<string> GetAllowedProcedureIds(out string error)
    {
        if (procedureRepository == null)
        {
            error = "ProcedureRepository is missing on GeminiLlmService.";
            return new List<string>();
        }

        if (!procedureRepository.TryGetAllProcedureIds(out List<string> result, out error))
        {
            return new List<string>();
        }

        error = null;
        return result;
    }

    private List<string> GetAvailableControlIds()
    {
        List<string> result = new List<string>();
        if (inputRegistry == null || inputRegistry.inputs == null)
        {
            return result;
        }

        for (int i = 0; i < inputRegistry.inputs.Count; i++)
        {
            CockpitInputData item = inputRegistry.inputs[i];
            if (item == null)
            {
                continue;
            }

            if (!string.IsNullOrWhiteSpace(item.inputId))
            {
                AddUniqueCaseInsensitive(result, item.inputId.Trim());
            }
        }

        return result;
    }

    private Dictionary<string, List<string>> BuildControlAliasMap(List<string> controlIds)
    {
        Dictionary<string, List<string>> aliases = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        Dictionary<string, string> canonical = BuildCanonicalLookup(controlIds);

        if (inputRegistry != null && inputRegistry.inputs != null)
        {
            for (int i = 0; i < inputRegistry.inputs.Count; i++)
            {
                CockpitInputData item = inputRegistry.inputs[i];
                if (item == null || string.IsNullOrWhiteSpace(item.inputId))
                {
                    continue;
                }

                string canonicalId = ResolveCanonicalId(item.inputId, canonical);
                if (string.IsNullOrWhiteSpace(canonicalId))
                {
                    continue;
                }

                AddAliasVariants(aliases, item.inputId, canonicalId);
                AddAliasVariants(aliases, item.displayName, canonicalId);
                AddAliasVariants(aliases, item.targetObjectName, canonicalId);
            }
        }

        for (int i = 0; i < aliasOverrides.Count; i++)
        {
            ControlAliasOverride entry = aliasOverrides[i];
            if (entry == null || string.IsNullOrWhiteSpace(entry.alias) || entry.controlIds == null)
            {
                continue;
            }

            for (int c = 0; c < entry.controlIds.Count; c++)
            {
                string canonicalId = ResolveCanonicalId(entry.controlIds[c], canonical);
                if (string.IsNullOrWhiteSpace(canonicalId))
                {
                    continue;
                }

                AddAliasVariants(aliases, entry.alias, canonicalId);
            }
        }

        AddDefaultLeverAliases(aliases, canonical);
        return aliases;
    }

    private static Dictionary<string, string> BuildCanonicalLookup(List<string> controlIds)
    {
        Dictionary<string, string> lookup = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (controlIds == null)
        {
            return lookup;
        }

        for (int i = 0; i < controlIds.Count; i++)
        {
            string id = controlIds[i];
            if (string.IsNullOrWhiteSpace(id))
            {
                continue;
            }

            string canonical = id.Trim();
            if (!lookup.ContainsKey(canonical))
            {
                lookup.Add(canonical, canonical);
            }

            string normalized = NormalizeAlias(canonical);
            if (normalized.Length > 0 && !lookup.ContainsKey(normalized))
            {
                lookup.Add(normalized, canonical);
            }
        }

        return lookup;
    }

    private static string ResolveCanonicalId(string candidate, Dictionary<string, string> canonicalLookup)
    {
        if (string.IsNullOrWhiteSpace(candidate) || canonicalLookup == null || canonicalLookup.Count == 0)
        {
            return null;
        }

        string trimmed = candidate.Trim();
        if (canonicalLookup.TryGetValue(trimmed, out string canonical))
        {
            return canonical;
        }

        string normalized = NormalizeAlias(trimmed);
        if (normalized.Length > 0 && canonicalLookup.TryGetValue(normalized, out canonical))
        {
            return canonical;
        }

        return null;
    }

    private static void AddAliasVariants(Dictionary<string, List<string>> aliases, string rawAlias, string canonicalControlId)
    {
        if (aliases == null || string.IsNullOrWhiteSpace(rawAlias) || string.IsNullOrWhiteSpace(canonicalControlId))
        {
            return;
        }

        string trimmed = rawAlias.Trim();
        AddAlias(aliases, trimmed, canonicalControlId);

        string normalized = NormalizeAlias(trimmed);
        if (normalized.Length > 0)
        {
            AddAlias(aliases, normalized, canonicalControlId);
        }

        string spaced = SplitCamelCase(trimmed);
        if (!string.IsNullOrWhiteSpace(spaced))
        {
            AddAlias(aliases, spaced, canonicalControlId);
            string normalizedSpaced = NormalizeAlias(spaced);
            if (normalizedSpaced.Length > 0)
            {
                AddAlias(aliases, normalizedSpaced, canonicalControlId);
            }
        }
    }

    private static void AddAlias(Dictionary<string, List<string>> aliases, string alias, string canonicalControlId)
    {
        if (string.IsNullOrWhiteSpace(alias) || string.IsNullOrWhiteSpace(canonicalControlId))
        {
            return;
        }

        if (!aliases.TryGetValue(alias, out List<string> targets))
        {
            targets = new List<string>();
            aliases.Add(alias, targets);
        }

        AddUniqueCaseInsensitive(targets, canonicalControlId);
    }

    private static void AddDefaultLeverAliases(Dictionary<string, List<string>> aliases, Dictionary<string, string> canonicalLookup)
    {
        string lever1 = ResolveCanonicalId("FuelControlLever1", canonicalLookup);
        string lever2 = ResolveCanonicalId("FuelControlLever2", canonicalLookup);
        if (string.IsNullOrWhiteSpace(lever1) || string.IsNullOrWhiteSpace(lever2))
        {
            return;
        }

        AddAliasVariants(aliases, "fuelControlLever", lever1);
        AddAliasVariants(aliases, "fuelControlLever", lever2);
        AddAliasVariants(aliases, "fuel control lever", lever1);
        AddAliasVariants(aliases, "fuel control lever", lever2);
        AddAliasVariants(aliases, "fuel lever", lever1);
        AddAliasVariants(aliases, "fuel lever", lever2);
    }

    private string ResolveApiKey()
    {
        if (!string.IsNullOrWhiteSpace(apiKeyOverride))
        {
            return apiKeyOverride.Trim();
        }

        return Environment.GetEnvironmentVariable(ApiKeyEnvironmentVariable);
    }

    private static void AddUniqueCaseInsensitive(List<string> values, string value)
    {
        if (values == null || string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        for (int i = 0; i < values.Count; i++)
        {
            if (string.Equals(values[i], value, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }
        }

        values.Add(value);
    }

    private static string NormalizeAlias(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        StringBuilder builder = new StringBuilder(value.Length);
        for (int i = 0; i < value.Length; i++)
        {
            char c = value[i];
            if (char.IsLetterOrDigit(c))
            {
                builder.Append(char.ToLowerInvariant(c));
            }
        }

        return builder.ToString();
    }

    private static string SplitCamelCase(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        StringBuilder builder = new StringBuilder(value.Length + 8);
        for (int i = 0; i < value.Length; i++)
        {
            char c = value[i];
            if (i > 0 && char.IsUpper(c) && char.IsLetterOrDigit(value[i - 1]))
            {
                builder.Append(' ');
            }

            builder.Append(c);
        }

        return builder.ToString();
    }

    [Serializable]
    private class ControlAliasOverride
    {
        public string alias;
        public List<string> controlIds = new List<string>();
    }
}
