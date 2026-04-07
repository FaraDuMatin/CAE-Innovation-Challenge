using UnityEngine;
using System.Collections.Generic;

public class MockLlmResponseRepository : MonoBehaviour
{
    [Header("Source")]
    [SerializeField] private TextAsset responseJson;

    [Header("Validation")]
    [SerializeField] private bool validateOnAwake = true;

    private MockLlmResponseFile cachedFile;

    public MockLlmResponseFile CachedFile => cachedFile;

    private void Awake()
    {
        if (!validateOnAwake)
        {
            return;
        }

        if (!TryLoad(out cachedFile, out string error))
        {
            Debug.LogError($"[MockLlmResponseRepository] {error}", this);
            return;
        }

        Debug.Log(
            $"[MockLlmResponseRepository] Loaded {cachedFile.Responses.Count} responses (schema v{cachedFile.schemaVersion}).",
            this);
    }

    public bool TryLoad(out MockLlmResponseFile file, out string error)
    {
        file = null;

        if (responseJson == null)
        {
            error = "responseJson TextAsset is not assigned.";
            return false;
        }

        string raw = responseJson.text;
        if (string.IsNullOrWhiteSpace(raw))
        {
            error = "responseJson is empty.";
            return false;
        }

        file = JsonUtility.FromJson<MockLlmResponseFile>(raw);
        if (!MockLlmResponseValidator.TryValidate(file, out error))
        {
            file = null;
            return false;
        }

        file.Normalize();

        return true;
    }

    public bool TryGetResponseByIndex(int index, out MockLlmResponse response, out string error)
    {
        response = null;

        if (!EnsureLoaded(out error))
        {
            return false;
        }

        List<MockLlmResponse> responses = cachedFile.Responses;
        if (index < 0 || index >= responses.Count)
        {
            error =
                $"Response index out of range: {index}. Valid range: 0..{responses.Count - 1}.";
            return false;
        }

        response = responses[index];
        error = null;
        return true;
    }

    public bool TryGetFirstByType(string responseType, out MockLlmResponse response, out string error)
    {
        response = null;

        if (string.IsNullOrWhiteSpace(responseType))
        {
            error = "responseType is required.";
            return false;
        }

        if (!EnsureLoaded(out error))
        {
            return false;
        }

        List<MockLlmResponse> responses = cachedFile.Responses;
        for (int i = 0; i < responses.Count; i++)
        {
            MockLlmResponse item = responses[i];
            if (item == null)
            {
                continue;
            }

            if (string.Equals(item.responseType, responseType, System.StringComparison.OrdinalIgnoreCase))
            {
                response = item;
                error = null;
                return true;
            }
        }

        error = $"No mock response found for type '{responseType}'.";
        return false;
    }

    public bool TryGetProcedureResponse(string procedureId, out MockLlmResponse response, out string error)
    {
        response = null;

        if (string.IsNullOrWhiteSpace(procedureId))
        {
            error = "procedureId is required.";
            return false;
        }

        if (!EnsureLoaded(out error))
        {
            return false;
        }

        List<MockLlmResponse> responses = cachedFile.Responses;
        for (int i = 0; i < responses.Count; i++)
        {
            MockLlmResponse item = responses[i];
            if (item == null)
            {
                continue;
            }

            if (!string.Equals(item.responseType, "procedure", System.StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (string.Equals(item.procedureId, procedureId, System.StringComparison.OrdinalIgnoreCase))
            {
                response = item;
                error = null;
                return true;
            }
        }

        error = $"No procedure mock response found for procedureId '{procedureId}'.";
        return false;
    }

    public bool TryGetAllResponses(out List<MockLlmResponse> responses, out string error)
    {
        responses = null;

        if (!EnsureLoaded(out error))
        {
            return false;
        }

        responses = cachedFile.Responses;
        error = null;
        return true;
    }

    private bool EnsureLoaded(out string error)
    {
        if (cachedFile != null)
        {
            error = null;
            return true;
        }

        return TryLoad(out cachedFile, out error);
    }
}
