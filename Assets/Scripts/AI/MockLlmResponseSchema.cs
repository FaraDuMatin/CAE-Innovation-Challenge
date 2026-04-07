using System;
using System.Collections.Generic;

[Serializable]
public class MockLlmResponseFile
{
    public int schemaVersion = 1;
    public string assistantName = "Atlas";
    public List<string> availableProcedureIds = new List<string>();
    public List<MockLlmResponse> mockResponses = new List<MockLlmResponse>();
    public List<MockLlmResponse> mockLlmResponses = new List<MockLlmResponse>();

    public List<MockLlmResponse> Responses =>
        mockResponses != null && mockResponses.Count > 0 ? mockResponses : mockLlmResponses;

    public void Normalize()
    {
        if (availableProcedureIds == null)
        {
            availableProcedureIds = new List<string>();
        }

        if (mockResponses == null)
        {
            mockResponses = new List<MockLlmResponse>();
        }

        if (mockLlmResponses == null)
        {
            mockLlmResponses = new List<MockLlmResponse>();
        }

        if (mockResponses.Count == 0 && mockLlmResponses.Count > 0)
        {
            mockResponses = mockLlmResponses;
        }
        else if (mockLlmResponses.Count == 0 && mockResponses.Count > 0)
        {
            mockLlmResponses = mockResponses;
        }
    }
}

[Serializable]
public class MockLlmResponse
{
    public string responseType;
    public string procedureId;
    public string textResponse;
    public int resumeFromStepIndex;
    public List<string> highlightControlIds = new List<string>();
    public string requestId;
    public long timestampMs;

    // Legacy field kept for compatibility with older test payloads.
    public List<MockControlStep> controls = new List<MockControlStep>();
}

[Serializable]
public class MockControlStep
{
    public string controlId;
    public bool waitForClick = true;
    public string instructionText;
}

public static class MockLlmResponseValidator
{
    public const int SupportedSchemaVersion = 1;

    private static readonly HashSet<string> SupportedResponseTypes =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "procedure",
            "simpleAnswer",
            "error"
        };

    public static bool TryValidate(MockLlmResponseFile file, out string error)
    {
        if (file == null)
        {
            error = "JSON parse failed: file is null.";
            return false;
        }

        file.Normalize();

        if (file.schemaVersion != SupportedSchemaVersion)
        {
            error = $"Unsupported schemaVersion '{file.schemaVersion}'. Expected {SupportedSchemaVersion}.";
            return false;
        }

        if (file.Responses == null || file.Responses.Count == 0)
        {
            error = "mockResponses/mockLlmResponses must contain at least one response object.";
            return false;
        }

        for (int i = 0; i < file.Responses.Count; i++)
        {
            MockLlmResponse response = file.Responses[i];
            if (!TryValidateResponse(response, i, out error))
            {
                return false;
            }
        }

        error = null;
        return true;
    }

    public static bool TryValidateSingleResponse(MockLlmResponse response, out string error)
    {
        MockLlmResponseFile wrapper = new MockLlmResponseFile
        {
            schemaVersion = SupportedSchemaVersion,
            mockResponses = new List<MockLlmResponse> { response }
        };

        return TryValidate(wrapper, out error);
    }

    private static bool TryValidateResponse(MockLlmResponse response, int index, out string error)
    {
        if (response == null)
        {
            error = $"mockResponses[{index}] is null.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(response.responseType))
        {
            error = $"mockResponses[{index}].responseType is required.";
            return false;
        }

        if (!SupportedResponseTypes.Contains(response.responseType))
        {
            error =
                $"mockResponses[{index}].responseType '{response.responseType}' is invalid. " +
                "Allowed: procedure, simpleAnswer, error.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(response.textResponse))
        {
            error = $"mockResponses[{index}].textResponse is required.";
            return false;
        }

        if (response.resumeFromStepIndex < 0)
        {
            error = $"mockResponses[{index}].resumeFromStepIndex cannot be negative.";
            return false;
        }

        bool isProcedure = string.Equals(response.responseType, "procedure", StringComparison.OrdinalIgnoreCase);
        if (isProcedure && string.IsNullOrWhiteSpace(response.procedureId))
        {
            error = $"mockResponses[{index}] is procedure but procedureId is missing.";
            return false;
        }

        if (response.highlightControlIds == null)
        {
            response.highlightControlIds = new List<string>();
        }

        if (response.controls == null)
        {
            response.controls = new List<MockControlStep>();
        }

        for (int c = 0; c < response.controls.Count; c++)
        {
            MockControlStep step = response.controls[c];
            if (step == null)
            {
                error = $"mockResponses[{index}].controls[{c}] is null.";
                return false;
            }
        }

        error = null;
        return true;
    }
}
