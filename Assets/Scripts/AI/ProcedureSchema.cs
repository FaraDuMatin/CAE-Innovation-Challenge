using System;
using System.Collections.Generic;

[Serializable]
public class ProcedureFile
{
    public int schemaVersion = 1;
    public string assistantName = "Atlas";
    public List<ProcedureDefinition> procedures = new List<ProcedureDefinition>();

    public void Normalize()
    {
        if (procedures == null)
        {
            procedures = new List<ProcedureDefinition>();
        }

        for (int i = 0; i < procedures.Count; i++)
        {
            ProcedureDefinition procedure = procedures[i];
            if (procedure == null)
            {
                continue;
            }

            if (procedure.steps == null)
            {
                procedure.steps = new List<ProcedureStep>();
            }
        }
    }
}

[Serializable]
public class ProcedureDefinition
{
    public string procedureId;
    public string displayName;
    public bool isMandatory;
    public List<ProcedureStep> steps = new List<ProcedureStep>();
}

[Serializable]
public class ProcedureStep
{
    public string stepType;
    public string controlId;
    public string action;
    public string monitorKey;
    public string instructionText;
    public bool waitForClick = true;
}

public static class ProcedureValidator
{
    public const int SupportedSchemaVersion = 1;

    public static bool TryValidate(ProcedureFile file, out string error)
    {
        if (file == null)
        {
            error = "JSON parse failed: procedure file is null.";
            return false;
        }

        file.Normalize();

        if (file.schemaVersion != SupportedSchemaVersion)
        {
            error = $"Unsupported procedure schemaVersion '{file.schemaVersion}'. Expected {SupportedSchemaVersion}.";
            return false;
        }

        if (file.procedures == null || file.procedures.Count == 0)
        {
            error = "procedures must contain at least one procedure.";
            return false;
        }

        for (int i = 0; i < file.procedures.Count; i++)
        {
            if (!TryValidateProcedure(file.procedures[i], i, out error))
            {
                return false;
            }
        }

        error = null;
        return true;
    }

    private static bool TryValidateProcedure(ProcedureDefinition procedure, int index, out string error)
    {
        if (procedure == null)
        {
            error = $"procedures[{index}] is null.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(procedure.procedureId))
        {
            error = $"procedures[{index}].procedureId is required.";
            return false;
        }

        if (procedure.steps == null || procedure.steps.Count == 0)
        {
            error = $"procedures[{index}].steps must contain at least one step.";
            return false;
        }

        for (int i = 0; i < procedure.steps.Count; i++)
        {
            ProcedureStep step = procedure.steps[i];
            if (step == null)
            {
                error = $"procedures[{index}].steps[{i}] is null.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(step.stepType))
            {
                error = $"procedures[{index}].steps[{i}].stepType is required.";
                return false;
            }

            if (string.Equals(step.stepType, "control", StringComparison.OrdinalIgnoreCase) &&
                string.IsNullOrWhiteSpace(step.controlId))
            {
                error = $"procedures[{index}].steps[{i}].controlId is required for control stepType.";
                return false;
            }
        }

        error = null;
        return true;
    }
}
