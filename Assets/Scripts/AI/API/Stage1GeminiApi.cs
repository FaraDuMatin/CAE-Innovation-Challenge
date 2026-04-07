using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

public sealed class Stage1GeminiApi
{
    private static readonly string[] AllowedResponseTypes = { "procedure", "simpleAnswer", "error" };

    public IEnumerator RequestIntent(
        string apiKey,
        GeminiApiRequestSettings requestSettings,
        Stage1RequestContext context,
        Action<Stage1IntentResult> onSuccess,
        Action<string> onError)
    {
        if (context == null)
        {
            onError?.Invoke("Stage-1 request context is missing.");
            yield break;
        }

        string prompt = BuildPrompt(context);

        string json = null;
        string requestError = null;
        yield return GeminiApiTransport.RequestJsonObject(
            apiKey,
            requestSettings,
            prompt,
            "Stage1GeminiApi",
            value => json = value,
            err => requestError = err);

        if (!string.IsNullOrWhiteSpace(requestError))
        {
            onError?.Invoke(requestError);
            yield break;
        }

        if (!TryParseIntent(json, context, out Stage1IntentResult intent, out string parseError))
        {
            onError?.Invoke(parseError);
            yield break;
        }

        onSuccess?.Invoke(intent);
    }

    private static string BuildPrompt(Stage1RequestContext context)
    {
        string procedures = context.availableProcedureIds == null || context.availableProcedureIds.Count == 0
            ? "none"
            : string.Join(", ", context.availableProcedureIds);

        string activeProcedure = string.IsNullOrWhiteSpace(context.activeProcedureId)
            ? "none"
            : context.activeProcedureId;

        return
            "You are Atlas in a Boeing VR cockpit trainer. " +
            "Classify user intent only. Return JSON only (no markdown) with this exact schema: " +
            "{\"responseType\":\"procedure|simpleAnswer|error\",\"procedureId\":\"string\",\"resumeFromStepIndex\":0,\"textResponse\":\"string\"}. " +
            "Allowed procedureId values: " + procedures + ". " +
            "Runtime context: " +
            "activeProcedureId=" + activeProcedure + ", " +
            "activeStepIndex=" + Mathf.Max(0, context.activeStepIndex) + ", " +
            "isProcedureRunning=" + context.isProcedureRunning + ", " +
            "isProcedurePaused=" + context.isProcedurePaused + ", " +
            "isWaitingForClick=" + context.isWaitingForClick + ". " +
            "Rules: " +
            "If the user asks to start/continue/progress a checklist, use responseType='procedure'. " +
            "If the user asks an informational side question while procedure is active, use responseType='simpleAnswer' and keep procedureId empty. " +
            "If procedure is chosen, procedureId must be one allowed value and resumeFromStepIndex >= 0. " +
            "If simpleAnswer or error is chosen, set procedureId to empty string and resumeFromStepIndex to 0. " +
            "textResponse must be short and safe for UI. " +
            "User command: " + context.userCommand;
    }

    private static bool TryParseIntent(
        string json,
        Stage1RequestContext context,
        out Stage1IntentResult intent,
        out string error)
    {
        intent = null;

        List<string> availableProcedureIds = context != null
            ? context.availableProcedureIds
            : null;

        try
        {
            intent = JsonUtility.FromJson<Stage1IntentResult>(json);
        }
        catch (Exception ex)
        {
            error = $"Stage-1 JSON parse failed: {ex.Message}";
            return false;
        }

        if (intent == null)
        {
            error = "Stage-1 result is null.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(intent.responseType))
        {
            error = "Stage-1 missing responseType.";
            return false;
        }

        bool typeSupported = false;
        for (int i = 0; i < AllowedResponseTypes.Length; i++)
        {
            if (string.Equals(intent.responseType, AllowedResponseTypes[i], StringComparison.OrdinalIgnoreCase))
            {
                intent.responseType = AllowedResponseTypes[i];
                typeSupported = true;
                break;
            }
        }

        if (!typeSupported)
        {
            error = $"Stage-1 returned unsupported responseType '{intent.responseType}'.";
            return false;
        }

        if (intent.resumeFromStepIndex < 0)
        {
            intent.resumeFromStepIndex = 0;
        }

        if (string.Equals(intent.responseType, "procedure", StringComparison.OrdinalIgnoreCase))
        {
            if (!IsAllowedProcedureId(intent.procedureId, availableProcedureIds))
            {
                if (TryApplyActiveProcedureFallback(intent, context, availableProcedureIds))
                {
                    Debug.LogWarning("[Stage1GeminiApi] Stage-1 procedureId invalid. Falling back to active procedure context.");
                }
                else
                {
                    error = "Stage-1 returned an invalid procedureId.";
                    return false;
                }
            }
        }
        else
        {
            intent.procedureId = string.Empty;
            intent.resumeFromStepIndex = 0;
        }

        if (string.IsNullOrWhiteSpace(intent.textResponse))
        {
            intent.textResponse = string.Equals(intent.responseType, "error", StringComparison.OrdinalIgnoreCase)
                ? "I could not map your request."
                : "Understood.";
        }

        error = null;
        return true;
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

    private static bool TryApplyActiveProcedureFallback(
        Stage1IntentResult intent,
        Stage1RequestContext context,
        List<string> availableProcedureIds)
    {
        if (intent == null || context == null)
        {
            return false;
        }

        bool hasActiveProcedure =
            (context.isProcedureRunning || context.isProcedurePaused) &&
            !string.IsNullOrWhiteSpace(context.activeProcedureId);

        if (!hasActiveProcedure)
        {
            return false;
        }

        if (!IsAllowedProcedureId(context.activeProcedureId, availableProcedureIds))
        {
            return false;
        }

        intent.procedureId = context.activeProcedureId;
        intent.resumeFromStepIndex = Mathf.Max(0, context.activeStepIndex);

        if (string.IsNullOrWhiteSpace(intent.textResponse))
        {
            intent.textResponse = "Continuing current procedure.";
        }

        return true;
    }
}

[Serializable]
public sealed class Stage1RequestContext
{
    public string userCommand;
    public List<string> availableProcedureIds = new List<string>();
    public string activeProcedureId;
    public int activeStepIndex;
    public bool isProcedureRunning;
    public bool isProcedurePaused;
    public bool isWaitingForClick;
}

[Serializable]
public sealed class Stage1IntentResult
{
    public string responseType;
    public string procedureId;
    public int resumeFromStepIndex;
    public string textResponse;
    public float confidence;
    public string reasonShort;
}

[Serializable]
public sealed class GeminiApiRequestSettings
{
    public string modelName;
    public float requestTimeoutSeconds;
    public float temperature;
    public int maxRetriesOnRateLimit;
    public float rateLimitBackoffBaseSeconds;
    public bool logRawModelResponse;
}

internal static class GeminiApiTransport
{
    private const string ApiBaseUrl = "https://generativelanguage.googleapis.com/v1beta/models";

    public static IEnumerator RequestJsonObject(
        string apiKey,
        GeminiApiRequestSettings settings,
        string prompt,
        string logTag,
        Action<string> onSuccess,
        Action<string> onError)
    {
        if (settings == null)
        {
            onError?.Invoke("Gemini request settings are missing.");
            yield break;
        }

        if (string.IsNullOrWhiteSpace(settings.modelName))
        {
            onError?.Invoke("Gemini model name is missing.");
            yield break;
        }

        string endpoint = $"{ApiBaseUrl}/{settings.modelName}:generateContent?key={apiKey}";
        string requestJson = JsonUtility.ToJson(CreateRequestBody(prompt, settings.temperature));

        int maxAttempts = Mathf.Max(1, settings.maxRetriesOnRateLimit + 1);
        for (int attempt = 0; attempt < maxAttempts; attempt++)
        {
            Debug.Log($"[{logTag}] Sending Gemini request. attempt={attempt + 1}/{maxAttempts}, model='{settings.modelName}', promptChars={prompt.Length}");

            using (UnityWebRequest request = new UnityWebRequest(endpoint, UnityWebRequest.kHttpVerbPOST))
            {
                byte[] payload = Encoding.UTF8.GetBytes(requestJson);
                request.uploadHandler = new UploadHandlerRaw(payload);
                request.downloadHandler = new DownloadHandlerBuffer();
                request.timeout = Mathf.CeilToInt(Mathf.Max(1f, settings.requestTimeoutSeconds));
                request.SetRequestHeader("Content-Type", "application/json");

                yield return request.SendWebRequest();

                string rawApiResponse = request.downloadHandler != null
                    ? request.downloadHandler.text
                    : string.Empty;

                Debug.Log($"[{logTag}] Gemini response received. http={request.responseCode}, unityResult={request.result}, rawChars={rawApiResponse.Length}");

                if (TryReadApiError(request.responseCode, rawApiResponse, out string apiError, out bool isRateLimited))
                {
                    Debug.LogWarning($"[{logTag}] API-level error detected. rateLimited={isRateLimited}, message='{apiError}'");

                    if (isRateLimited && attempt < maxAttempts - 1)
                    {
                        float delay = Mathf.Max(0.1f, settings.rateLimitBackoffBaseSeconds) * Mathf.Pow(2f, attempt);
                        Debug.LogWarning($"[{logTag}] Backing off for {delay:0.00}s before retry.");
                        yield return new WaitForSeconds(delay);
                        continue;
                    }

                    onError?.Invoke(apiError);
                    yield break;
                }

                if (request.result != UnityWebRequest.Result.Success)
                {
                    string networkError = string.IsNullOrWhiteSpace(request.error)
                        ? "Unknown network error."
                        : request.error;
                    onError?.Invoke($"Gemini request failed: {networkError}");
                    yield break;
                }

                if (string.IsNullOrWhiteSpace(rawApiResponse))
                {
                    onError?.Invoke("Gemini returned an empty response.");
                    yield break;
                }

                if (!TryExtractModelJson(rawApiResponse, out string modelJson, out string extractError))
                {
                    onError?.Invoke(extractError);
                    yield break;
                }

                if (settings.logRawModelResponse)
                {
                    Debug.Log($"[{logTag}] Raw model JSON: {modelJson}");
                }

                onSuccess?.Invoke(modelJson);
                yield break;
            }
        }

        onError?.Invoke("Gemini request failed after retries.");
    }

    private static GeminiGenerateContentRequest CreateRequestBody(string prompt, float temperature)
    {
        return new GeminiGenerateContentRequest
        {
            contents = new[]
            {
                new GeminiContent
                {
                    parts = new[] { new GeminiPart { text = prompt } }
                }
            },
            generationConfig = new GeminiGenerationConfig
            {
                temperature = temperature,
                responseMimeType = "application/json",
                maxOutputTokens = 300
            }
        };
    }

    private static bool TryReadApiError(long statusCode, string rawApiResponse, out string error, out bool isRateLimited)
    {
        error = null;
        isRateLimited = false;

        GeminiGenerateContentResponse parsed = null;
        if (!string.IsNullOrWhiteSpace(rawApiResponse))
        {
            try
            {
                parsed = JsonUtility.FromJson<GeminiGenerateContentResponse>(rawApiResponse);
            }
            catch
            {
                parsed = null;
            }
        }

        if (parsed != null && HasApiErrorData(parsed.error))
        {
            string status = string.IsNullOrWhiteSpace(parsed.error.status) ? "unknown" : parsed.error.status;
            string message = string.IsNullOrWhiteSpace(parsed.error.message) ? "No message" : parsed.error.message;
            error = $"Gemini API error ({status}): {message}";
            isRateLimited = status.IndexOf("RESOURCE_EXHAUSTED", StringComparison.OrdinalIgnoreCase) >= 0 ||
                            statusCode == 429;
            return true;
        }

        if (statusCode == 429)
        {
            error = "Gemini API rate limit reached (HTTP 429).";
            isRateLimited = true;
            return true;
        }

        if (statusCode >= 400)
        {
            error = $"Gemini HTTP error {statusCode}.";
            isRateLimited = false;
            return true;
        }

        return false;
    }

    private static bool TryExtractModelJson(string apiResponse, out string modelJson, out string error)
    {
        modelJson = null;
        error = null;

        GeminiGenerateContentResponse parsed;
        try
        {
            parsed = JsonUtility.FromJson<GeminiGenerateContentResponse>(apiResponse);
        }
        catch (Exception ex)
        {
            error = $"Failed to parse Gemini API envelope: {ex.Message}";
            return false;
        }

        if (parsed == null)
        {
            error = "Gemini API envelope is null.";
            return false;
        }

        if (HasApiErrorData(parsed.error))
        {
            string status = string.IsNullOrWhiteSpace(parsed.error.status) ? "unknown" : parsed.error.status;
            string message = string.IsNullOrWhiteSpace(parsed.error.message) ? "No message" : parsed.error.message;
            error = $"Gemini API error ({status}): {message}";
            return false;
        }

        if (parsed.candidates == null || parsed.candidates.Length == 0)
        {
            error = "Gemini returned no candidates.";
            return false;
        }

        GeminiCandidate first = parsed.candidates[0];
        if (first == null || first.content == null || first.content.parts == null || first.content.parts.Length == 0)
        {
            error = "Gemini candidate contains no text parts.";
            return false;
        }

        string text = first.content.parts[0] != null ? first.content.parts[0].text : null;
        if (string.IsNullOrWhiteSpace(text))
        {
            error = "Gemini candidate text is empty.";
            return false;
        }

        string trimmed = text.Trim();
        if (trimmed.StartsWith("```", StringComparison.Ordinal))
        {
            trimmed = trimmed
                .Replace("```json", string.Empty, StringComparison.OrdinalIgnoreCase)
                .Replace("```", string.Empty, StringComparison.Ordinal)
                .Trim();
        }

        int firstBrace = trimmed.IndexOf('{');
        int lastBrace = trimmed.LastIndexOf('}');
        if (firstBrace < 0 || lastBrace < firstBrace)
        {
            error = "Gemini did not return a JSON object.";
            return false;
        }

        modelJson = trimmed.Substring(firstBrace, lastBrace - firstBrace + 1);
        return true;
    }

    private static bool HasApiErrorData(GeminiApiError apiError)
    {
        if (apiError == null)
        {
            return false;
        }

        if (apiError.code != 0)
        {
            return true;
        }

        if (!string.IsNullOrWhiteSpace(apiError.status))
        {
            return true;
        }

        if (!string.IsNullOrWhiteSpace(apiError.message))
        {
            return true;
        }

        return false;
    }

    [Serializable]
    private class GeminiGenerateContentRequest
    {
        public GeminiContent[] contents;
        public GeminiGenerationConfig generationConfig;
    }

    [Serializable]
    private class GeminiGenerationConfig
    {
        public float temperature;
        public string responseMimeType;
        public int maxOutputTokens;
    }

    [Serializable]
    private class GeminiGenerateContentResponse
    {
        public GeminiCandidate[] candidates;
        public GeminiApiError error;
    }

    [Serializable]
    private class GeminiCandidate
    {
        public GeminiContent content;
    }

    [Serializable]
    private class GeminiContent
    {
        public GeminiPart[] parts;
    }

    [Serializable]
    private class GeminiPart
    {
        public string text;
    }

    [Serializable]
    private class GeminiApiError
    {
        public int code;
        public string message;
        public string status;
    }
}
