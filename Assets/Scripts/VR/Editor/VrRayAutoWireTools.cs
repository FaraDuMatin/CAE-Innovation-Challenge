#if UNITY_EDITOR
using System.Collections.Generic;
using Oculus.Interaction;
using Oculus.Interaction.Surfaces;
using UnityEditor;
using UnityEngine;

public static class VrRayAutoWireTools
{
    [MenuItem("CAE/VR/Auto-Wire Ray Interactables (Selected Controls)")]
    private static void AutoWireSelectedControls()
    {
        List<GameObject> targets = new List<GameObject>();
        GameObject[] selected = Selection.gameObjects;

        for (int i = 0; i < selected.Length; i++)
        {
            GameObject go = selected[i];
            if (go == null)
            {
                continue;
            }

            if (IsControlTarget(go))
            {
                targets.Add(go);
            }
        }

        RunAutoWire(targets, "selected controls");
    }

    [MenuItem("CAE/VR/Auto-Wire Ray Interactables (Selected Hierarchies)")]
    private static void AutoWireSelectedHierarchies()
    {
        List<GameObject> targets = new List<GameObject>();
        HashSet<GameObject> seen = new HashSet<GameObject>();

        GameObject[] selected = Selection.gameObjects;
        for (int i = 0; i < selected.Length; i++)
        {
            GameObject root = selected[i];
            if (root == null)
            {
                continue;
            }

            Transform[] all = root.GetComponentsInChildren<Transform>(true);
            for (int t = 0; t < all.Length; t++)
            {
                GameObject go = all[t].gameObject;
                if (go == null || seen.Contains(go) || !IsControlTarget(go))
                {
                    continue;
                }

                seen.Add(go);
                targets.Add(go);
            }
        }

        RunAutoWire(targets, "selected hierarchies");
    }

    private static bool IsControlTarget(GameObject go)
    {
        return go.GetComponent<CockpitInputBinding>() != null ||
               go.GetComponent<InputInteraction>() != null;
    }

    private static void RunAutoWire(List<GameObject> targets, string scope)
    {
        if (targets == null || targets.Count == 0)
        {
            Debug.LogWarning($"[VrRayAutoWireTools] No control targets found in {scope}.");
            return;
        }

        int processed = 0;
        int skippedNoCollider = 0;

        Undo.SetCurrentGroupName("Auto-Wire Ray Interactables");
        int undoGroup = Undo.GetCurrentGroup();

        for (int i = 0; i < targets.Count; i++)
        {
            GameObject go = targets[i];
            if (go == null)
            {
                continue;
            }

            Collider collider = go.GetComponent<Collider>();
            if (collider == null)
            {
                skippedNoCollider++;
                continue;
            }

            ColliderSurface surface = go.GetComponent<ColliderSurface>();
            if (surface == null)
            {
                surface = Undo.AddComponent<ColliderSurface>(go);
            }

            SerializedObject soSurface = new SerializedObject(surface);
            SerializedProperty pCollider = soSurface.FindProperty("_collider");
            pCollider.objectReferenceValue = collider;
            soSurface.ApplyModifiedProperties();
            EditorUtility.SetDirty(surface);

            RayInteractable rayInteractable = go.GetComponent<RayInteractable>();
            if (rayInteractable == null)
            {
                rayInteractable = Undo.AddComponent<RayInteractable>(go);
            }

            SerializedObject soRay = new SerializedObject(rayInteractable);
            SerializedProperty pSurface = soRay.FindProperty("_surface");
            pSurface.objectReferenceValue = surface;
            soRay.ApplyModifiedProperties();
            EditorUtility.SetDirty(rayInteractable);

            InteractableUnityEventWrapper wrapper = go.GetComponent<InteractableUnityEventWrapper>();
            if (wrapper == null)
            {
                wrapper = Undo.AddComponent<InteractableUnityEventWrapper>(go);
            }

            SerializedObject soWrapper = new SerializedObject(wrapper);
            SerializedProperty pView = soWrapper.FindProperty("_interactableView");
            pView.objectReferenceValue = rayInteractable;
            soWrapper.ApplyModifiedProperties();
            EditorUtility.SetDirty(wrapper);

            processed++;
        }

        Undo.CollapseUndoOperations(undoGroup);

        Debug.Log(
            $"[VrRayAutoWireTools] Done. Processed: {processed}, skipped (no collider): {skippedNoCollider}, scope: {scope}.");
    }
}
#endif
