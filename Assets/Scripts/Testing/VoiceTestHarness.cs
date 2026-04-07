using System.Collections;
using Meta.WitAi.Dictation;
using TMPro;
using UnityEngine;

/// <summary>
/// Push-to-talk voice input: STT via Dictation → feeds into TestVoiceInput.SubmitInput().
/// TTS is handled on the output side by MockResponseProcedureRunner.
/// </summary>
public class VoiceTestHarness : MonoBehaviour
{
    [Header("Voice SDK — STT")]
    [SerializeField] private DictationService dictation;

    [Header("AI Pipeline")]
    [SerializeField] private TestVoiceInput testVoiceInput;

    [Header("UI Output")]
    [SerializeField] private TMP_Text transcriptLabel;
    [SerializeField] private TMP_Text statusLabel;

    [Header("Timing")]
    [Tooltip("How long to record before sending to Wit.ai (seconds).")]
    [SerializeField] [Min(1f)] private float listenDurationSeconds = 4f;

    private bool busy;

    // ─── Lifecycle ────────────────────────────────────────────────

    private void OnEnable()
    {
        if (dictation == null)
        {
            Debug.LogError("[VoiceTestHarness] DictationService not assigned!", this);
            return;
        }

        dictation.DictationEvents.OnStartListening.AddListener(OnMicStarted);
        dictation.DictationEvents.OnStoppedListening.AddListener(OnMicStopped);
        dictation.DictationEvents.OnPartialTranscription.AddListener(OnPartial);
        dictation.DictationEvents.OnFullTranscription.AddListener(OnFull);
        dictation.DictationEvents.OnError.AddListener(OnError);

        Debug.Log("[VoiceTestHarness] Ready. Press button to talk.", this);
        SetStatus("Press button to talk.");
    }

    private void OnDisable()
    {
        if (dictation == null) return;
        dictation.DictationEvents.OnStartListening.RemoveListener(OnMicStarted);
        dictation.DictationEvents.OnStoppedListening.RemoveListener(OnMicStopped);
        dictation.DictationEvents.OnPartialTranscription.RemoveListener(OnPartial);
        dictation.DictationEvents.OnFullTranscription.RemoveListener(OnFull);
        dictation.DictationEvents.OnError.RemoveListener(OnError);
        StopAllCoroutines();
    }

    private void Update()
    {
        // B button on right controller (Button.Two = B/Y, we use right hand)
        if (OVRInput.GetDown(OVRInput.Button.Two))
        {
            OnRecordButtonPressed();
        }
    }

    // ─── Button ───────────────────────────────────────────────────

    /// <summary>Wire to a UI Button OnClick or triggered by controller B button.</summary>
    public void OnRecordButtonPressed()
    {
        if (busy)
        {
            Debug.Log("[VoiceTestHarness] Busy — ignoring button press.");
            return;
        }

        if (dictation == null)
        {
            Debug.LogError("[VoiceTestHarness] DictationService not assigned!", this);
            return;
        }

        StartCoroutine(RecordRoutine());
    }

    // ─── Recording ─────────────────────────────────────────────────

    private IEnumerator RecordRoutine()
    {
        busy = true;
        SetTranscript(string.Empty);

        Debug.Log("[VoiceTestHarness] Starting mic (ActivateImmediately)...", this);
        SetStatus($"Recording for {listenDurationSeconds}s — speak now!");
        dictation.ActivateImmediately();

        yield return new WaitForSeconds(listenDurationSeconds);

        Debug.Log("[VoiceTestHarness] Stopping mic → sending to Wit.ai...", this);
        SetStatus("Sending to Wit.ai...");
        dictation.Deactivate();

        // Wait for OnFull to fire (max 10s safety)
        float waited = 0f;
        while (busy && waited < 10f)
        {
            waited += Time.deltaTime;
            yield return null;
        }

        if (busy)
        {
            Debug.LogWarning("[VoiceTestHarness] Timed out waiting for transcription.");
            SetStatus("Timed out. Press button to retry.");
            busy = false;
        }
    }

    // ─── Dictation Events ──────────────────────────────────────────

    private void OnMicStarted()
    {
        Debug.Log("[VoiceTestHarness] Mic started.");
    }

    private void OnMicStopped()
    {
        Debug.Log("[VoiceTestHarness] Mic stopped.");
    }

    private void OnPartial(string text)
    {
        Debug.Log($"[VoiceTestHarness] Partial: \"{text}\"");
        SetTranscript(text);
    }

    private void OnFull(string text)
    {
        Debug.Log($"[VoiceTestHarness] Full transcription: \"{text}\"");
        SetTranscript(text);
        busy = false;

        if (string.IsNullOrWhiteSpace(text))
        {
            Debug.LogWarning("[VoiceTestHarness] Empty transcription.");
            SetStatus("Nothing heard. Try again.");
            return;
        }

        // Feed the transcription into the existing AI pipeline.
        // TestVoiceInput handles wake word extraction + Gemini call + procedure runner.
        if (testVoiceInput != null)
        {
            Debug.Log($"[VoiceTestHarness] Sending to AI pipeline: \"{text}\"");
            SetStatus("Sending to Atlas...");
            testVoiceInput.SubmitInput(text);
        }
        else
        {
            Debug.LogError("[VoiceTestHarness] TestVoiceInput not assigned!", this);
            SetStatus("TestVoiceInput missing!");
        }
    }

    private void OnError(string error, string message)
    {
        Debug.LogError($"[VoiceTestHarness] Dictation error — {error}: {message}", this);
        SetStatus($"Error: {message}\nPress button to retry.");
        busy = false;
    }

    // ─── Helpers ─────────────────────────────────────────────────

    private void SetTranscript(string text)
    {
        if (transcriptLabel != null) transcriptLabel.text = text;
    }

    private void SetStatus(string text)
    {
        if (statusLabel != null) statusLabel.text = text;
    }
}
