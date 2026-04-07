using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

public sealed class Stage2GeminiApi
{
    public IEnumerator RequestPayload(
        string apiKey,
        GeminiApiRequestSettings requestSettings,
        Stage2RequestContext context,
        Action<MockLlmResponse> onSuccess,
        Action<string> onError)
    {
        if (context == null)
        {
            onError?.Invoke("Stage-2 request context is missing.");
            yield break;
        }

        if (context.stage1Intent == null)
        {
            onError?.Invoke("Stage-2 missing stage-1 intent.");
            yield break;
        }

        string expectedResponseType = context.stage1Intent.responseType;
        if (!string.Equals(expectedResponseType, "procedure", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(expectedResponseType, "simpleAnswer", StringComparison.OrdinalIgnoreCase))
        {
            onError?.Invoke($"Stage-2 cannot build payload for responseType '{expectedResponseType}'.");
            yield break;
        }

        string prompt = string.Equals(expectedResponseType, "procedure", StringComparison.OrdinalIgnoreCase)
            ? BuildProcedurePayloadPrompt(context)
            : BuildSimpleAnswerPayloadPrompt(context);

        string json = null;
        string requestError = null;
        yield return GeminiApiTransport.RequestJsonObject(
            apiKey,
            requestSettings,
            prompt,
            "Stage2GeminiApi",
            value => json = value,
            err => requestError = err);

        if (!string.IsNullOrWhiteSpace(requestError))
        {
            onError?.Invoke(requestError);
            yield break;
        }

        if (!TryParsePayload(json, context, out MockLlmResponse payload, out string parseError))
        {
            onError?.Invoke(parseError);
            yield break;
        }

        onSuccess?.Invoke(payload);
    }

    public static MockLlmResponse BuildErrorResponse(string message)
    {
        return new MockLlmResponse
        {
            responseType = "error",
            textResponse = string.IsNullOrWhiteSpace(message)
                ? "I could not map your request to a valid action."
                : message,
            procedureId = string.Empty,
            resumeFromStepIndex = 0,
            highlightControlIds = new List<string>()
        };
    }

    private static string BuildProcedurePayloadPrompt(Stage2RequestContext context)
    {
        return
            "Generate final procedure payload only. Return JSON only with this exact schema: " +
            "{\"responseType\":\"procedure\",\"procedureId\":\"string\",\"textResponse\":\"string\",\"resumeFromStepIndex\":0}. " +
            "Use procedureId exactly: " + context.stage1Intent.procedureId + ". " +
            "Do not include procedure steps, control lists, explanations, or extra keys. " +
            "Keep textResponse to one short sentence. " +
            "resumeFromStepIndex should be >= 0 and usually " + context.stage1Intent.resumeFromStepIndex + ". " +
            "User command: " + context.userCommand;
    }

    private static string BuildSimpleAnswerPayloadPrompt(Stage2RequestContext context)
    {
        string knownControls = context.availableControlIds == null || context.availableControlIds.Count == 0
            ? "none"
            : string.Join(", ", context.availableControlIds);

        string aliasHints = BuildAliasHints(context.controlAliases);

        return
            "Generate final simpleAnswer payload only. Return JSON only with this exact schema: " +
            "{\"responseType\":\"simpleAnswer\",\"textResponse\":\"string\",\"highlightControlIds\":[\"string\"]}. " +
            "Do not include procedure steps or extra keys. " +
            "Known control IDs in this cockpit are: " + knownControls + ". " +
            "Alias hints (user phrase => control IDs): " + aliasHints + ". " +
            "If the user asks about a known control, include matching control IDs in highlightControlIds. " +
            "If uncertain, return an empty highlightControlIds array. " +
            "Keep textResponse concise and practical. " +
            "User command: " + context.userCommand + ". " +
            "Stage-1 hint: " + context.stage1Intent.textResponse;
    }

    private static bool TryParsePayload(string json, Stage2RequestContext context, out MockLlmResponse response, out string error)
    {
        response = null;

        try
        {
            response = JsonUtility.FromJson<MockLlmResponse>(json);
        }
        catch (Exception ex)
        {
            error = $"Stage-2 JSON parse failed: {ex.Message}";
            return false;
        }

        if (response == null)
        {
            error = "Stage-2 result is null.";
            return false;
        }

        response.responseType = context.stage1Intent.responseType;

        if (string.Equals(response.responseType, "procedure", StringComparison.OrdinalIgnoreCase))
        {
            if (!IsAllowedProcedureId(response.procedureId, context.availableProcedureIds))
            {
                response.procedureId = context.stage1Intent.procedureId;
            }

            if (!IsAllowedProcedureId(response.procedureId, context.availableProcedureIds))
            {
                error = "Stage-2 procedure payload has invalid procedureId.";
                return false;
            }

            if (response.resumeFromStepIndex < 0)
            {
                response.resumeFromStepIndex = Mathf.Max(0, context.stage1Intent.resumeFromStepIndex);
            }
        }
        else
        {
            response.procedureId = string.Empty;
            response.resumeFromStepIndex = 0;

            EnsureSimpleAnswerHighlights(response, context);
        }

        if (string.IsNullOrWhiteSpace(response.textResponse))
        {
            response.textResponse = context.stage1Intent.textResponse;
        }

        if (response.highlightControlIds == null)
        {
            response.highlightControlIds = new List<string>();
        }

        if (!MockLlmResponseValidator.TryValidateSingleResponse(response, out error))
        {
            response = null;
            return false;
        }

        return true;
    }

    private static void EnsureSimpleAnswerHighlights(MockLlmResponse payload, Stage2RequestContext context)
    {
        if (payload.highlightControlIds == null)
        {
            payload.highlightControlIds = new List<string>();
        }

        Dictionary<string, string> lookup = BuildCanonicalControlLookup(context.availableControlIds);
        List<string> result = new List<string>();

        for (int i = 0; i < payload.highlightControlIds.Count; i++)
        {
            if (TryResolveControlId(payload.highlightControlIds[i], lookup, out string canonical))
            {
                AddUnique(result, canonical);
            }
        }

        if (result.Count == 0)
        {
            ApplyAliasFallbacks(result, context.userCommand, context.controlAliases, lookup);
        }

        if (!context.allowMultipleHighlights && result.Count > 1)
        {
            result = new List<string> { result[0] };
        }

        payload.highlightControlIds = result;
    }

    private static void ApplyAliasFallbacks(
        List<string> result,
        string userCommand,
        Dictionary<string, List<string>> controlAliases,
        Dictionary<string, string> lookup)
    {
        if (string.IsNullOrWhiteSpace(userCommand))
        {
            return;
        }

        string normalizedCommand = NormalizeKey(userCommand);

        if (controlAliases != null)
        {
            foreach (KeyValuePair<string, List<string>> entry in controlAliases)
            {
                if (string.IsNullOrWhiteSpace(entry.Key) || entry.Value == null || entry.Value.Count == 0)
                {
                    continue;
                }

                if (!ContainsAlias(userCommand, normalizedCommand, entry.Key))
                {
                    continue;
                }

                for (int i = 0; i < entry.Value.Count; i++)
                {
                    if (TryResolveControlId(entry.Value[i], lookup, out string canonical))
                    {
                        AddUnique(result, canonical);
                    }
                }
            }
        }

        if (result.Count > 0)
        {
            return;
        }

        foreach (KeyValuePair<string, string> entry in lookup)
        {
            if (ContainsAlias(userCommand, normalizedCommand, entry.Key))
            {
                AddUnique(result, entry.Value);
            }
        }
    }

    private static bool ContainsAlias(string rawText, string normalizedText, string alias)
    {
        if (string.IsNullOrWhiteSpace(alias))
        {
            return false;
        }

        if (rawText.IndexOf(alias, StringComparison.OrdinalIgnoreCase) >= 0)
        {
            return true;
        }

        string normalizedAlias = NormalizeKey(alias);
        return normalizedAlias.Length > 0 && normalizedText.Contains(normalizedAlias, StringComparison.Ordinal);
    }

    private static Dictionary<string, string> BuildCanonicalControlLookup(List<string> availableControlIds)
    {
        Dictionary<string, string> lookup = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (availableControlIds == null)
        {
            return lookup;
        }

        for (int i = 0; i < availableControlIds.Count; i++)
        {
            string id = availableControlIds[i];
            if (string.IsNullOrWhiteSpace(id))
            {
                continue;
            }

            string canonical = id.Trim();

            if (!lookup.ContainsKey(canonical))
            {
                lookup.Add(canonical, canonical);
            }

            string normalized = NormalizeKey(canonical);
            if (normalized.Length > 0 && !lookup.ContainsKey(normalized))
            {
                lookup.Add(normalized, canonical);
            }
        }

        return lookup;
    }

    private static bool TryResolveControlId(string rawControlId, Dictionary<string, string> lookup, out string canonical)
    {
        canonical = null;

        if (string.IsNullOrWhiteSpace(rawControlId) || lookup == null || lookup.Count == 0)
        {
            return false;
        }

        string trimmed = rawControlId.Trim();
        if (lookup.TryGetValue(trimmed, out canonical))
        {
            return true;
        }

        string normalized = NormalizeKey(trimmed);
        if (normalized.Length > 0 && lookup.TryGetValue(normalized, out canonical))
        {
            return true;
        }

        return false;
    }

    private static string NormalizeKey(string value)
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

    private static void AddUnique(List<string> values, string value)
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

    private static bool IsAllowedProcedureId(string procedureId, List<string> availableProcedureIds)
    {
        if (string.IsNullOrWhiteSpace(procedureId) || availableProcedureIds == null || availableProcedureIds.Count == 0)
        {
            return false;
        }

        for (int i = 0; i < availableProcedureIds.Count; i++)
        {
            if (string.Equals(procedureId, availableProcedureIds[i], StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static string BuildAliasHints(Dictionary<string, List<string>> controlAliases)
    {
        if (controlAliases == null || controlAliases.Count == 0)
        {
            return "none";
        }

        StringBuilder builder = new StringBuilder();
        int count = 0;

        foreach (KeyValuePair<string, List<string>> entry in controlAliases)
        {
            if (string.IsNullOrWhiteSpace(entry.Key) || entry.Value == null || entry.Value.Count == 0)
            {
                continue;
            }

            if (count > 0)
            {
                builder.Append("; ");
            }

            builder.Append(entry.Key);
            builder.Append(" => ");
            builder.Append(string.Join("|", entry.Value));

            count++;
            if (count >= 24)
            {
                break;
            }
        }

        return count == 0 ? "none" : builder.ToString();
    }
}

[Serializable]
public sealed class Stage2RequestContext
{
    public string userCommand;
    public Stage1IntentResult stage1Intent;
    public List<string> availableProcedureIds = new List<string>();
    public List<string> availableControlIds = new List<string>();
    public Dictionary<string, List<string>> controlAliases = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
    public bool allowMultipleHighlights = true;
}
