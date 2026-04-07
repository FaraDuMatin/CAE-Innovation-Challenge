using System.Collections;
using System.Collections.Generic;
using System;
using UnityEngine;

public class InputInteraction : MonoBehaviour
{
    public static event Action<string> ControlActivated;

    [Header("Data")]
    [SerializeField] private CockpitInputBinding inputBinding;

    [Header("Visual Feedback")]
    [SerializeField] private Renderer[] glowRenderers;
    [SerializeField] private Color glowColor = Color.cyan;
    [SerializeField] [Min(0f)] private float glowIntensity = 2f;
    [SerializeField] [Min(0.01f)] private float glowDuration = 0.15f;
    [SerializeField] private bool useOverlayMaterial;
    [SerializeField] private Material overlayMaterial;

    [Header("Audio Feedback")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip interactionClip;
    [SerializeField] [Range(0f, 1f)] private float volume = 1f;

    private readonly List<MaterialState> materialStates = new();
    private readonly Dictionary<Renderer, Material[]> originalRendererMaterials = new();
    private Coroutine glowRoutine;

    private static readonly int EmissionColor = Shader.PropertyToID("_EmissionColor");
    private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId = Shader.PropertyToID("_Color");

    private void Reset()
    {
        if (inputBinding == null)
        {
            inputBinding = GetComponent<CockpitInputBinding>();
        }

        if (glowRenderers == null || glowRenderers.Length == 0)
        {
            Renderer r = GetComponent<Renderer>();
            if (r != null)
            {
                glowRenderers = new[] { r };
            }
        }

        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
        }
    }

    public void TriggerInteractionFeedback()
    {
        TriggerGlowOnly();

        if (audioSource != null && interactionClip != null)
        {
            audioSource.PlayOneShot(interactionClip, volume);
        }
    }

    public void TriggerGlowOnly()
    {
        if (glowRenderers == null || glowRenderers.Length == 0)
        {
            return;
        }

        StopGlowAndRestore();

        if (useOverlayMaterial && overlayMaterial != null)
        {
            CaptureOriginalRendererMaterials();
        }
        else
        {
            CaptureCurrentMaterialState();
        }

        glowRoutine = StartCoroutine(GlowRoutine());
    }

    public void StartStepHighlight()
    {
        if (glowRenderers == null || glowRenderers.Length == 0)
        {
            return;
        }

        StopGlowAndRestore();

        if (useOverlayMaterial && overlayMaterial != null)
        {
            CaptureOriginalRendererMaterials();
            ApplyOverlayMaterial();
            return;
        }

        CaptureCurrentMaterialState();
        ApplyGlow(glowColor * glowIntensity);
    }

    public void StopGlowAndRestore()
    {
        if (glowRoutine != null)
        {
            StopCoroutine(glowRoutine);
            glowRoutine = null;
        }

        RestoreVisualState();
    }

    public void OnClickToggle01()
    {
        if (inputBinding == null || inputBinding.InputData == null)
        {
            TriggerInteractionFeedback();
            NotifyControlActivated();
            return;
        }

        float min = inputBinding.InputData.minValue;
        float max = inputBinding.InputData.maxValue;
        float mid = (min + max) * 0.5f;
        float next = inputBinding.InputData.currentValue > mid ? min : max;

        inputBinding.SetNormalizedValue(next);
        TriggerInteractionFeedback();
        NotifyControlActivated();
    }

    public void OnClickSetNormalized(float value)
    {
        if (inputBinding != null)
        {
            inputBinding.SetNormalizedValue(value);
        }

        TriggerInteractionFeedback();
        NotifyControlActivated();
    }

    [ContextMenu("Test Feedback")]
    private void TestFeedback()
    {
        TriggerInteractionFeedback();
    }

    private IEnumerator GlowRoutine()
    {
        if (useOverlayMaterial && overlayMaterial != null)
        {
            ApplyOverlayMaterial();
            yield return new WaitForSeconds(glowDuration);
            RestoreVisualState();
            glowRoutine = null;
            yield break;
        }

        float elapsed = 0f;
        Color targetEmission = glowColor * glowIntensity;

        while (elapsed < glowDuration)
        {
            elapsed += Time.deltaTime;
            float t = 1f - (elapsed / glowDuration);
            ApplyGlow(targetEmission * t);
            yield return null;
        }

        RestoreVisualState();
        glowRoutine = null;
    }

    private void CaptureOriginalRendererMaterials()
    {
        originalRendererMaterials.Clear();

        foreach (Renderer r in glowRenderers)
        {
            if (r == null)
            {
                continue;
            }

            originalRendererMaterials[r] = r.materials;
        }
    }

    private void ApplyOverlayMaterial()
    {
        foreach (Renderer r in glowRenderers)
        {
            if (r == null)
            {
                continue;
            }

            Material[] current = r.materials;
            if (current == null || current.Length == 0)
            {
                continue;
            }

            Material[] overlay = new Material[current.Length];
            for (int i = 0; i < overlay.Length; i++)
            {
                overlay[i] = overlayMaterial;
            }

            r.materials = overlay;
        }
    }

    private void RestoreRendererMaterials()
    {
        foreach (KeyValuePair<Renderer, Material[]> entry in originalRendererMaterials)
        {
            if (entry.Key != null)
            {
                entry.Key.materials = entry.Value;
            }
        }

        originalRendererMaterials.Clear();
    }

    private void RestoreVisualState()
    {
        RestoreRendererMaterials();
        RestoreMaterials();
    }

    private void CaptureCurrentMaterialState()
    {
        materialStates.Clear();

        foreach (Renderer r in glowRenderers)
        {
            if (r == null)
            {
                continue;
            }

            Material[] mats = r.materials;
            for (int i = 0; i < mats.Length; i++)
            {
                Material m = mats[i];
                if (m == null)
                {
                    continue;
                }

                MaterialState s = new MaterialState
                {
                    material = m,
                    hasEmission = m.HasProperty(EmissionColor),
                    hasBaseColor = m.HasProperty(BaseColor),
                    hasColor = m.HasProperty(ColorId)
                };

                if (s.hasEmission)
                {
                    s.emission = m.GetColor(EmissionColor);
                }

                if (s.hasBaseColor)
                {
                    s.baseColor = m.GetColor(BaseColor);
                }

                if (s.hasColor)
                {
                    s.color = m.GetColor(ColorId);
                }

                materialStates.Add(s);
            }
        }
    }

    private void ApplyGlow(Color emission)
    {
        for (int i = 0; i < materialStates.Count; i++)
        {
            MaterialState s = materialStates[i];
            if (s.material == null)
            {
                continue;
            }

            if (s.hasEmission)
            {
                s.material.EnableKeyword("_EMISSION");
                s.material.SetColor(EmissionColor, emission);
            }

            if (s.hasBaseColor)
            {
                s.material.SetColor(BaseColor, s.baseColor + (glowColor * 0.12f));
            }
            else if (s.hasColor)
            {
                s.material.SetColor(ColorId, s.color + (glowColor * 0.12f));
            }
        }
    }

    private void RestoreMaterials()
    {
        for (int i = 0; i < materialStates.Count; i++)
        {
            MaterialState s = materialStates[i];
            if (s.material == null)
            {
                continue;
            }

            if (s.hasEmission)
            {
                s.material.SetColor(EmissionColor, s.emission);
            }

            if (s.hasBaseColor)
            {
                s.material.SetColor(BaseColor, s.baseColor);
            }

            if (s.hasColor)
            {
                s.material.SetColor(ColorId, s.color);
            }
        }
    }

    private void NotifyControlActivated()
    {
        string controlId = null;
        if (inputBinding != null && inputBinding.InputData != null)
        {
            controlId = inputBinding.InputData.inputId;
        }

        if (string.IsNullOrWhiteSpace(controlId))
        {
            controlId = gameObject.name;
        }

        ControlActivated?.Invoke(controlId);
    }

    private struct MaterialState
    {
        public Material material;
        public bool hasEmission;
        public bool hasBaseColor;
        public bool hasColor;
        public Color emission;
        public Color baseColor;
        public Color color;
    }
}
