using System;
using UnityEngine;

public class MockRequestResolver : MonoBehaviour
{
    [SerializeField] private GeminiLlmService geminiLlmService;

    private void Reset()
    {
        if (geminiLlmService == null)
        {
            geminiLlmService = FindFirstObjectByType<GeminiLlmService>();
        }
    }

    public void TryResolveAsync(
        string commandText,
        Action<MockLlmResponse> onSuccess,
        Action<string> onError)
    {
        if (geminiLlmService == null)
        {
            onError?.Invoke("GeminiLlmService is missing.");
            return;
        }

        string query = string.IsNullOrWhiteSpace(commandText)
            ? string.Empty
            : commandText.Trim();
        if (query.Length == 0)
        {
            onError?.Invoke("Command text is empty.");
            return;
        }

        geminiLlmService.RequestResponse(query, onSuccess, onError);
    }
}
