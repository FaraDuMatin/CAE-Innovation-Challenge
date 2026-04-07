using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class HighlightPart : MonoBehaviour
{
    [SerializeField] private CockpitInputRegistry inputRegistry;
    [SerializeField] private bool caseInsensitiveIds = true;
    [SerializeField] private bool includeInactiveObjects = true;
    [SerializeField] private bool rebuildOnEnable = true;

    [Header("Yoke Special Case")]
    [SerializeField] private string yokeControlId = "Yoke";
    [SerializeField] private string[] yokePartObjectNames = { "Yoke", "YokeBehind", "YokeBottom", "YokeRight", "YokeLeft" };

    private Dictionary<string, InputInteraction> byId;
    private Dictionary<string, InputInteraction> byObjectName;
    private bool lookupBuilt;
    private InputInteraction currentStepHighlight;
    private readonly List<InputInteraction> currentYokeHighlights = new List<InputInteraction>();

    private StringComparison Comparison =>
        caseInsensitiveIds ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

    private void OnEnable()
    {
        if (rebuildOnEnable)
        {
            RebuildLookup();
        }
    }

    public void RebuildLookup()
    {
        StringComparer comparer = caseInsensitiveIds
            ? StringComparer.OrdinalIgnoreCase
            : StringComparer.Ordinal;

        byId = new Dictionary<string, InputInteraction>(comparer);
        byObjectName = new Dictionary<string, InputInteraction>(comparer);

        foreach (CockpitInputBinding binding in GetBindings())
        {
            if (binding == null)
            {
                continue;
            }

            InputInteraction interaction = binding.GetComponent<InputInteraction>();
            if (interaction == null)
            {
                continue;
            }

            CockpitInputData data = binding.InputData;
            if (data == null)
            {
                continue;
            }

            AddIfValid(byId, data.inputId, interaction);
            AddIfValid(byObjectName, data.targetObjectName, interaction);
            AddIfValid(byObjectName, binding.gameObject.name, interaction);
        }

        lookupBuilt = true;
    }

    public bool HighlightById(string id)
    {
        if (IsYokeControl(id))
        {
            return HighlightYokeGlowOnly();
        }

        if (!TryResolveInteraction(id, out InputInteraction interaction))
        {
            return false;
        }

        interaction.TriggerGlowOnly();
        return true;
    }

    public bool HighlightStepById(string id)
    {
        if (IsYokeControl(id))
        {
            return HighlightYokeStep();
        }

        if (!TryResolveInteraction(id, out InputInteraction interaction))
        {
            return false;
        }

        if (currentStepHighlight != null && currentStepHighlight != interaction)
        {
            currentStepHighlight.StopGlowAndRestore();
        }

        interaction.StartStepHighlight();
        currentStepHighlight = interaction;
        return true;
    }

    public void ClearCurrentStepHighlight()
    {
        if (currentStepHighlight != null)
        {
            currentStepHighlight.StopGlowAndRestore();
            currentStepHighlight = null;
        }

        for (int i = 0; i < currentYokeHighlights.Count; i++)
        {
            InputInteraction interaction = currentYokeHighlights[i];
            if (interaction != null)
            {
                interaction.StopGlowAndRestore();
            }
        }

        currentYokeHighlights.Clear();
    }

    public void HighlightByIdFromEvent(string id)
    {
        HighlightById(id);
    }

    private IEnumerable<CockpitInputBinding> GetBindings()
    {
        if (!includeInactiveObjects)
        {
            return FindObjectsByType<CockpitInputBinding>(FindObjectsSortMode.None);
        }

        List<CockpitInputBinding> result = new List<CockpitInputBinding>();
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            Scene scene = SceneManager.GetSceneAt(i);
            if (!scene.isLoaded)
            {
                continue;
            }

            GameObject[] roots = scene.GetRootGameObjects();
            for (int r = 0; r < roots.Length; r++)
            {
                result.AddRange(roots[r].GetComponentsInChildren<CockpitInputBinding>(true));
            }
        }

        return result;
    }

    private CockpitInputData FindInRegistry(string query)
    {
        if (inputRegistry == null || inputRegistry.inputs == null)
        {
            return null;
        }

        for (int i = 0; i < inputRegistry.inputs.Count; i++)
        {
            CockpitInputData item = inputRegistry.inputs[i];
            if (item == null)
            {
                continue;
            }

            if (!string.IsNullOrWhiteSpace(item.inputId) &&
                string.Equals(item.inputId, query, Comparison))
            {
                return item;
            }

            if (!string.IsNullOrWhiteSpace(item.targetObjectName) &&
                string.Equals(item.targetObjectName, query, Comparison))
            {
                return item;
            }
        }

        return null;
    }

    private bool TryResolveInteraction(string id, out InputInteraction interaction)
    {
        interaction = null;

        string query = id == null ? string.Empty : id.Trim();
        if (query.Length == 0)
        {
            return false;
        }

        if (!lookupBuilt || byId == null || byObjectName == null)
        {
            RebuildLookup();
        }

        if (TryGetInteraction(byId, query, out interaction))
        {
            return true;
        }

        if (TryGetInteraction(byObjectName, query, out interaction))
        {
            return true;
        }

        CockpitInputData registryMatch = FindInRegistry(query);
        if (registryMatch != null)
        {
            if (TryGetInteraction(byId, registryMatch.inputId, out interaction))
            {
                return true;
            }

            if (TryGetInteraction(byObjectName, registryMatch.targetObjectName, out interaction))
            {
                return true;
            }
        }

        // Late scene/object lifecycle can invalidate an early cache build. Rebuild once and retry.
        RebuildLookup();
        return TryGetInteraction(byId, query, out interaction) ||
               TryGetInteraction(byObjectName, query, out interaction);
    }

    private bool IsYokeControl(string id)
    {
        if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(yokeControlId))
        {
            return false;
        }

        return string.Equals(id.Trim(), yokeControlId, Comparison);
    }

    private bool HighlightYokeGlowOnly()
    {
        if (!TryResolveYokeParts(out List<InputInteraction> yokeParts))
        {
            return false;
        }

        for (int i = 0; i < yokeParts.Count; i++)
        {
            InputInteraction interaction = yokeParts[i];
            if (interaction != null)
            {
                interaction.TriggerGlowOnly();
            }
        }

        return true;
    }

    private bool HighlightYokeStep()
    {
        if (!TryResolveYokeParts(out List<InputInteraction> yokeParts))
        {
            return false;
        }

        ClearCurrentStepHighlight();

        for (int i = 0; i < yokeParts.Count; i++)
        {
            InputInteraction interaction = yokeParts[i];
            if (interaction != null)
            {
                interaction.StartStepHighlight();
                currentYokeHighlights.Add(interaction);
            }
        }

        return currentYokeHighlights.Count > 0;
    }

    private bool TryResolveYokeParts(out List<InputInteraction> interactions)
    {
        interactions = new List<InputInteraction>();

        if (!lookupBuilt || byId == null || byObjectName == null)
        {
            RebuildLookup();
        }

        if (yokePartObjectNames == null || yokePartObjectNames.Length == 0)
        {
            return false;
        }

        for (int i = 0; i < yokePartObjectNames.Length; i++)
        {
            string partName = yokePartObjectNames[i];
            if (string.IsNullOrWhiteSpace(partName))
            {
                continue;
            }

            if (!TryGetInteraction(byObjectName, partName, out InputInteraction interaction))
            {
                continue;
            }

            if (interaction == null || interactions.Contains(interaction))
            {
                continue;
            }

            interactions.Add(interaction);
        }

        if (interactions.Count > 0)
        {
            return true;
        }

        RebuildLookup();

        for (int i = 0; i < yokePartObjectNames.Length; i++)
        {
            string partName = yokePartObjectNames[i];
            if (string.IsNullOrWhiteSpace(partName))
            {
                continue;
            }

            if (!TryGetInteraction(byObjectName, partName, out InputInteraction interaction))
            {
                continue;
            }

            if (interaction == null || interactions.Contains(interaction))
            {
                continue;
            }

            interactions.Add(interaction);
        }

        return interactions.Count > 0;
    }

    private static void AddIfValid(
        Dictionary<string, InputInteraction> map,
        string key,
        InputInteraction interaction)
    {
        if (interaction == null || string.IsNullOrWhiteSpace(key))
        {
            return;
        }

        if (!map.ContainsKey(key))
        {
            map.Add(key, interaction);
        }
    }

    private static bool TryGetInteraction(
        Dictionary<string, InputInteraction> map,
        string key,
        out InputInteraction interaction)
    {
        interaction = null;

        if (map == null || string.IsNullOrWhiteSpace(key))
        {
            return false;
        }

        if (map.TryGetValue(key, out InputInteraction resolved) && resolved != null)
        {
            interaction = resolved;
            return true;
        }

        return false;
    }
}
