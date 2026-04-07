using System;
using TMPro;
using UnityEngine;

public class TestVoiceInput : MonoBehaviour
{
    [Header("Input")]
    [SerializeField] private TMP_InputField inputField;
    [SerializeField] private string assistantName = "Atlas";

    [Header("Dependencies")]
    [SerializeField] private MockRequestResolver mockRequestResolver;
    [SerializeField] private MockResponseProcedureRunner procedureRunner;

    [Header("UI Output")]
    [SerializeField] private TMP_Text statusText;
    [SerializeField] private bool clearInputAfterSubmit = true;

    private bool requestInFlight;

    private void Reset()
    {
        if (inputField == null)
        {
            inputField = GetComponent<TMP_InputField>();
        }

        if (mockRequestResolver == null)
        {
            mockRequestResolver = FindFirstObjectByType<MockRequestResolver>();
        }

        if (procedureRunner == null)
        {
            procedureRunner = FindFirstObjectByType<MockResponseProcedureRunner>();
        }
    }

    public void SubmitCurrentInput()
    {
        if (inputField == null)
        {
            SetStatus("InputField missing");
            return;
        }

        SubmitInput(inputField.text);
    }

    public void SubmitInput(string rawInput)
    {
        if (requestInFlight)
        {
            SetStatus("Please wait, request in progress...");
            return;
        }

        if (mockRequestResolver == null)
        {
            SetStatus("MockRequestResolver missing");
            return;
        }

        if (procedureRunner == null)
        {
            SetStatus("MockResponseProcedureRunner missing");
            return;
        }

        string query = rawInput == null ? string.Empty : rawInput.Trim();
        if (query.Length == 0)
        {
            SetStatus("Type Atlas and your command");
            return;
        }

        if (!TryExtractWakeCommand(query, out string command))
        {
            SetStatus($"Wake name not found: {assistantName}");
            return;
        }

        if (command.Length == 0)
        {
            SetStatus("Command missing after Atlas");
            return;
        }

        requestInFlight = true;
        SetStatus("Atlas heard. Calling Gemini...");

        if (clearInputAfterSubmit && inputField != null)
        {
            inputField.text = string.Empty;
            inputField.ActivateInputField();
        }

        mockRequestResolver.TryResolveAsync(
            command,
            response =>
            {
                requestInFlight = false;

                if (!procedureRunner.TryRunResponse(response, out string runError))
                {
                    SetStatus($"Runner rejected response: {runError}");
                    return;
                }

                SetStatus($"Atlas response: {response.responseType}");
            },
            error =>
            {
                requestInFlight = false;
                SetStatus($"LLM error: {error}");
            });
    }

    private bool TryExtractWakeCommand(string rawInput, out string command)
    {
        command = string.Empty;

        string name = string.IsNullOrWhiteSpace(assistantName)
            ? "Atlas"
            : assistantName.Trim();

        int startIndex = 0;
        while (true)
        {
            int index = rawInput.IndexOf(name, startIndex, StringComparison.OrdinalIgnoreCase);
            if (index < 0)
            {
                return false;
            }

            int end = index + name.Length;
            bool leftBoundary = index == 0 || !char.IsLetterOrDigit(rawInput[index - 1]);
            bool rightBoundary = end >= rawInput.Length || !char.IsLetterOrDigit(rawInput[end]);

            if (leftBoundary && rightBoundary)
            {
                string withoutWakeWord = rawInput.Remove(index, name.Length);
                command = withoutWakeWord.Trim(' ', ',', ':', ';', '-', '.').Trim();
                return true;
            }

            startIndex = end;
        }
    }

    private void SetStatus(string message)
    {
        if (statusText != null)
        {
            statusText.text = message;
        }
    }
}
