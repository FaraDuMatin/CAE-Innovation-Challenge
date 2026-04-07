using TMPro;
using UnityEngine;

public class TestHighlight : MonoBehaviour
{
    [SerializeField] private TMP_InputField inputField;
    [SerializeField] private HighlightPart highlighter;
    [SerializeField] private TMP_Text statusText;
    [SerializeField] private bool clearInputAfterSuccess = true;

    private void Reset()
    {
        if (inputField == null)
        {
            inputField = GetComponent<TMP_InputField>();
        }

        if (highlighter == null)
        {
            highlighter = FindFirstObjectByType<HighlightPart>();
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
        if (highlighter == null)
        {
            SetStatus("Highlighter missing");
            return;
        }

        string query = rawInput == null ? string.Empty : rawInput.Trim();
        if (query.Length == 0)
        {
            SetStatus("Type a part ID");
            return;
        }

        bool ok = highlighter.HighlightById(query);
        SetStatus(ok ? $"Highlighted: {query}" : $"Not found: {query}");

        if (ok && clearInputAfterSuccess && inputField != null)
        {
            inputField.text = string.Empty;
            inputField.ActivateInputField();
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
