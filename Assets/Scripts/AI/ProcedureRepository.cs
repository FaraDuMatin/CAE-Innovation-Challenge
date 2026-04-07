using System;
using System.Collections.Generic;
using UnityEngine;

public class ProcedureRepository : MonoBehaviour
{
    [Header("Source")]
    [SerializeField] private TextAsset procedureJson;

    [Header("Validation")]
    [SerializeField] private bool validateOnAwake = true;

    private ProcedureFile cachedFile;

    private void Awake()
    {
        if (!validateOnAwake)
        {
            return;
        }

        if (!TryLoad(out cachedFile, out string error))
        {
            Debug.LogError($"[ProcedureRepository] {error}", this);
            return;
        }

        Debug.Log(
            $"[ProcedureRepository] Loaded {cachedFile.procedures.Count} procedures (schema v{cachedFile.schemaVersion}).",
            this);
    }

    public bool TryLoad(out ProcedureFile file, out string error)
    {
        file = null;

        if (procedureJson == null)
        {
            error = "procedureJson TextAsset is not assigned.";
            return false;
        }

        string raw = procedureJson.text;
        if (string.IsNullOrWhiteSpace(raw))
        {
            error = "procedureJson is empty.";
            return false;
        }

        file = JsonUtility.FromJson<ProcedureFile>(raw);
        if (!ProcedureValidator.TryValidate(file, out error))
        {
            file = null;
            return false;
        }

        file.Normalize();
        return true;
    }

    public bool TryGetProcedureById(string procedureId, out ProcedureDefinition procedure, out string error)
    {
        procedure = null;

        if (string.IsNullOrWhiteSpace(procedureId))
        {
            error = "procedureId is required.";
            return false;
        }

        if (!EnsureLoaded(out error))
        {
            return false;
        }

        for (int i = 0; i < cachedFile.procedures.Count; i++)
        {
            ProcedureDefinition item = cachedFile.procedures[i];
            if (item == null)
            {
                continue;
            }

            if (string.Equals(item.procedureId, procedureId, StringComparison.OrdinalIgnoreCase))
            {
                procedure = item;
                error = null;
                return true;
            }
        }

        error = $"Procedure not found: '{procedureId}'.";
        return false;
    }

    public bool TryGetAllProcedureIds(out List<string> procedureIds, out string error)
    {
        procedureIds = new List<string>();

        if (!EnsureLoaded(out error))
        {
            return false;
        }

        for (int i = 0; i < cachedFile.procedures.Count; i++)
        {
            ProcedureDefinition item = cachedFile.procedures[i];
            if (item == null || string.IsNullOrWhiteSpace(item.procedureId))
            {
                continue;
            }

            bool exists = false;
            for (int j = 0; j < procedureIds.Count; j++)
            {
                if (string.Equals(procedureIds[j], item.procedureId, StringComparison.OrdinalIgnoreCase))
                {
                    exists = true;
                    break;
                }
            }

            if (!exists)
            {
                procedureIds.Add(item.procedureId);
            }
        }

        if (procedureIds.Count == 0)
        {
            error = "No procedure IDs available in procedure repository.";
            return false;
        }

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
