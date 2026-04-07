using System;
using System.Collections;
using System.Collections.Generic;
using Meta.WitAi.TTS.Utilities;
using TMPro;
using UnityEngine;

public class MockResponseProcedureRunner : MonoBehaviour
{
    [Header("Dependencies")]
    [SerializeField] private MockLlmResponseRepository repository;
    [SerializeField] private ProcedureRepository procedureRepository;
    [SerializeField] private HighlightPart highlighter;

    [Header("Voice Output (Optional)")]
    [Tooltip("If assigned, text responses will be spoken aloud via TTS.")]
    [SerializeField] private TTSSpeaker ttsSpeaker;

    [Header("UI Output (Optional)")]
    [SerializeField] private TMP_Text textResponseLabel;
    [SerializeField] private TMP_Text stepStatusLabel;

    [Header("Run Defaults")]
    [SerializeField] private int defaultResponseIndex;
    [SerializeField] [Min(0f)] private float nonBlockingStepDelay = 0.15f;
    [SerializeField] [Min(0f)] private float simpleAnswerHighlightSeconds = 3f;
    [SerializeField] [Min(0f)] private float clickWaitTimeoutSeconds;
    [SerializeField] private bool caseInsensitiveControlMatch = true;
    [SerializeField] private bool autoResumeAfterSimpleAnswer = true;

    [Header("Input Source")]
    [SerializeField] private bool useLiveInputEvents;

    private enum RunnerMode
    {
        Idle,
        Procedure,
        SimpleAnswer,
        Error
    }

    private Coroutine runRoutine;
    private string awaitedControlId;
    private bool awaitedControlClicked;
    private bool stepWaitTimedOut;

    private RunnerMode mode = RunnerMode.Idle;

    private string currentProcedureId;
    private int currentStepIndex;
    private string currentStepControlId;

    private bool hasPausedProcedure;
    private string pausedProcedureId;
    private int pausedStepIndex;

    public bool IsWaitingForClick =>
        !string.IsNullOrWhiteSpace(awaitedControlId) && !awaitedControlClicked;

    public string AwaitedControlId => awaitedControlId;
    public bool IsProcedureRunning => mode == RunnerMode.Procedure;
    public bool IsProcedurePaused => hasPausedProcedure;

    public string ActiveProcedureId =>
        IsProcedureRunning
            ? currentProcedureId
            : (hasPausedProcedure ? pausedProcedureId : string.Empty);

    public int ActiveStepIndex =>
        IsProcedureRunning
            ? Mathf.Max(0, currentStepIndex)
            : (hasPausedProcedure ? Mathf.Max(0, pausedStepIndex) : 0);

    public string ActiveStepControlId =>
        IsProcedureRunning ? currentStepControlId : string.Empty;

    private StringComparison IdComparison =>
        caseInsensitiveControlMatch ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

    private void Reset()
    {
        if (repository == null)
        {
            repository = FindFirstObjectByType<MockLlmResponseRepository>();
        }

        if (procedureRepository == null)
        {
            procedureRepository = FindFirstObjectByType<ProcedureRepository>();
        }

        if (highlighter == null)
        {
            highlighter = FindFirstObjectByType<HighlightPart>();
        }
    }

    private void OnEnable()
    {
        if (useLiveInputEvents)
        {
            InputInteraction.ControlActivated += OnControlActivated;
        }
    }

    private void OnDisable()
    {
        if (useLiveInputEvents)
        {
            InputInteraction.ControlActivated -= OnControlActivated;
        }

        StopExecution();
    }

    public void RunDefaultResponse()
    {
        RunResponseByIndex(defaultResponseIndex);
    }

    public void RunResponseByIndex(int index)
    {
        if (repository == null)
        {
            SetStepStatus("Repository missing");
            Debug.LogError("[MockResponseProcedureRunner] Repository is not assigned.", this);
            return;
        }

        if (!repository.TryGetResponseByIndex(index, out MockLlmResponse response, out string error))
        {
            SetStepStatus("Load failed");
            SetTextResponse(error);
            Debug.LogError($"[MockResponseProcedureRunner] {error}", this);
            return;
        }

        if (!TryRunResponse(response, out string runError))
        {
            SetStepStatus("Run failed");
            SetTextResponse(runError);
        }
    }

    public void RunFirstProcedureResponse()
    {
        RunFirstByType("procedure");
    }

    public void RunFirstSimpleAnswerResponse()
    {
        RunFirstByType("simpleAnswer");
    }

    public void RunResponse(MockLlmResponse response)
    {
        TryRunResponse(response, out _);
    }

    public bool TryRunResponse(MockLlmResponse response, out string error)
    {
        if (response == null)
        {
            SetStepStatus("Response missing");
            SetTextResponse("Invalid response payload.");
            error = "Response is null.";
            return false;
        }

        if (!MockLlmResponseValidator.TryValidateSingleResponse(response, out error))
        {
            SetStepStatus("Invalid response format");
            SetTextResponse(error);
            return false;
        }

        bool isProcedure = string.Equals(response.responseType, "procedure", StringComparison.OrdinalIgnoreCase);
        bool isSimpleAnswer = string.Equals(response.responseType, "simpleAnswer", StringComparison.OrdinalIgnoreCase);
        bool isError = string.Equals(response.responseType, "error", StringComparison.OrdinalIgnoreCase);

        if (isError && IsProcedureRunning)
        {
            SetTextResponse(response.textResponse);
            SetStepStatus("Assistant error. Procedure continues.");
            error = null;
            return true;
        }

        bool resumeAfterSimpleAnswer = false;
        if (isSimpleAnswer && IsProcedureRunning)
        {
            resumeAfterSimpleAnswer = PauseActiveProcedure();
        }

        if (isProcedure)
        {
            ClearPausedProcedureState();
        }

        StartExecution(response, resumeAfterSimpleAnswer);

        error = null;
        return true;
    }

    public void StopExecution()
    {
        StopCurrentRoutineOnly();

        if (highlighter != null)
        {
            highlighter.ClearCurrentStepHighlight();
        }

        mode = RunnerMode.Idle;
        ClearWaitState();
        ClearCurrentProcedureState();
        ClearPausedProcedureState();
    }

    public void SimulateExpectedClick()
    {
        if (!IsWaitingForClick)
        {
            return;
        }

        awaitedControlClicked = true;
    }

    private void RunFirstByType(string responseType)
    {
        if (repository == null)
        {
            SetStepStatus("Repository missing");
            Debug.LogError("[MockResponseProcedureRunner] Repository is not assigned.", this);
            return;
        }

        if (!repository.TryGetFirstByType(responseType, out MockLlmResponse response, out string error))
        {
            SetStepStatus("Load failed");
            SetTextResponse(error);
            Debug.LogError($"[MockResponseProcedureRunner] {error}", this);
            return;
        }

        if (!TryRunResponse(response, out string runError))
        {
            SetStepStatus("Run failed");
            SetTextResponse(runError);
        }
    }

    private void StartExecution(MockLlmResponse response, bool resumeAfterSimpleAnswer)
    {
        StopCurrentRoutineOnly();
        runRoutine = StartCoroutine(ExecuteResponseRoutine(response, resumeAfterSimpleAnswer));
    }

    private void StopCurrentRoutineOnly()
    {
        if (runRoutine != null)
        {
            StopCoroutine(runRoutine);
            runRoutine = null;
        }
    }

    private bool PauseActiveProcedure()
    {
        if (!IsProcedureRunning || string.IsNullOrWhiteSpace(currentProcedureId))
        {
            return false;
        }

        hasPausedProcedure = true;
        pausedProcedureId = currentProcedureId;
        pausedStepIndex = Mathf.Max(0, currentStepIndex);

        StopCurrentRoutineOnly();

        if (highlighter != null)
        {
            highlighter.ClearCurrentStepHighlight();
        }

        ClearWaitState();
        mode = RunnerMode.Idle;
        return true;
    }

    private IEnumerator ExecuteResponseRoutine(MockLlmResponse response, bool resumeAfterSimpleAnswer)
    {
        if (response == null)
        {
            SetStepStatus("Response missing");
            SetTextResponse("Invalid response payload.");
            runRoutine = null;
            yield break;
        }

        SetTextResponse(response.textResponse);
        yield return WaitForTtsToFinish();

        if (string.Equals(response.responseType, "error", StringComparison.OrdinalIgnoreCase))
        {
            mode = RunnerMode.Error;
            if (highlighter != null)
            {
                highlighter.ClearCurrentStepHighlight();
            }

            SetStepStatus("Error response");
            mode = RunnerMode.Idle;
            runRoutine = null;
            yield break;
        }

        if (string.Equals(response.responseType, "simpleAnswer", StringComparison.OrdinalIgnoreCase))
        {
            mode = RunnerMode.SimpleAnswer;
            yield return ExecuteSimpleAnswerRoutine(response);

            if (resumeAfterSimpleAnswer && autoResumeAfterSimpleAnswer)
            {
                yield return WaitForTtsToFinish();
                yield return ResumePausedProcedureRoutine();
            }
            else
            {
                mode = RunnerMode.Idle;
            }

            runRoutine = null;
            yield break;
        }

        if (string.Equals(response.responseType, "procedure", StringComparison.OrdinalIgnoreCase))
        {
            yield return ExecuteProcedureRoutine(response);
            runRoutine = null;
            yield break;
        }

        SetStepStatus($"Unsupported responseType: {response.responseType}");
        if (highlighter != null)
        {
            highlighter.ClearCurrentStepHighlight();
        }

        mode = RunnerMode.Idle;
        runRoutine = null;
    }

    private IEnumerator ExecuteSimpleAnswerRoutine(MockLlmResponse response)
    {
        List<string> highlightIds = GetDistinctControlIds(response.highlightControlIds);
        if (highlightIds.Count == 0)
        {
            SetStepStatus("Answer ready");
            yield break;
        }

        if (highlighter == null)
        {
            SetStepStatus("Highlighter missing");
            Debug.LogError("[MockResponseProcedureRunner] HighlightPart reference is missing.", this);
            yield break;
        }

        float totalDuration = Mathf.Max(0f, simpleAnswerHighlightSeconds);
        float segmentDuration = highlightIds.Count > 0
            ? (highlightIds.Count == 1 ? totalDuration : Mathf.Max(0.4f, totalDuration / highlightIds.Count))
            : totalDuration;

        int successCount = 0;
        for (int i = 0; i < highlightIds.Count; i++)
        {
            string controlId = highlightIds[i];
            bool highlighted = highlighter.HighlightStepById(controlId);
            if (!highlighted)
            {
                continue;
            }

            successCount++;
            SetStepStatus($"Highlighted: {controlId} ({i + 1}/{highlightIds.Count})");

            if (segmentDuration > 0f)
            {
                yield return new WaitForSeconds(segmentDuration);
            }

            highlighter.ClearCurrentStepHighlight();
        }

        if (successCount == 0)
        {
            SetStepStatus("Controls not found for highlighting");
        }
        else
        {
            SetStepStatus("Answer completed");
        }
    }

    private IEnumerator ResumePausedProcedureRoutine()
    {
        if (!hasPausedProcedure || string.IsNullOrWhiteSpace(pausedProcedureId))
        {
            mode = RunnerMode.Idle;
            yield break;
        }

        MockLlmResponse resumeResponse = new MockLlmResponse
        {
            responseType = "procedure",
            procedureId = pausedProcedureId,
            resumeFromStepIndex = Mathf.Max(0, pausedStepIndex),
            textResponse = "Resuming current procedure.",
            highlightControlIds = new List<string>()
        };

        ClearPausedProcedureState();

        SetTextResponse(resumeResponse.textResponse);
        yield return ExecuteProcedureRoutine(resumeResponse);
    }

    private IEnumerator ExecuteProcedureRoutine(MockLlmResponse response)
    {
        mode = RunnerMode.Procedure;

        if (procedureRepository == null)
        {
            SetStepStatus("Procedure repository missing");
            Debug.LogError("[MockResponseProcedureRunner] ProcedureRepository is not assigned.", this);
            mode = RunnerMode.Idle;
            yield break;
        }

        if (highlighter == null)
        {
            SetStepStatus("Highlighter missing");
            Debug.LogError("[MockResponseProcedureRunner] HighlightPart reference is missing.", this);
            mode = RunnerMode.Idle;
            yield break;
        }

        if (string.IsNullOrWhiteSpace(response.procedureId))
        {
            SetStepStatus("Missing procedureId");
            mode = RunnerMode.Idle;
            yield break;
        }

        if (!procedureRepository.TryGetProcedureById(response.procedureId, out ProcedureDefinition procedure, out string error))
        {
            SetStepStatus("Procedure load failed");
            SetTextResponse(error);
            Debug.LogError($"[MockResponseProcedureRunner] {error}", this);
            mode = RunnerMode.Idle;
            yield break;
        }

        if (procedure.steps == null || procedure.steps.Count == 0)
        {
            SetStepStatus("Procedure has no steps");
            highlighter.ClearCurrentStepHighlight();
            mode = RunnerMode.Idle;
            yield break;
        }

        int startIndex = Mathf.Max(0, response.resumeFromStepIndex);
        if (startIndex >= procedure.steps.Count)
        {
            SetStepStatus("resumeFromStepIndex out of range");
            highlighter.ClearCurrentStepHighlight();
            mode = RunnerMode.Idle;
            yield break;
        }

        currentProcedureId = response.procedureId;
        currentStepIndex = startIndex;
        currentStepControlId = string.Empty;

        for (int i = startIndex; i < procedure.steps.Count; i++)
        {
            ProcedureStep step = procedure.steps[i];
            if (step == null)
            {
                highlighter.ClearCurrentStepHighlight();
                SetStepStatus($"Invalid step at index {i}");
                mode = RunnerMode.Idle;
                ClearCurrentProcedureState();
                yield break;
            }

            currentStepIndex = i;
            currentStepControlId = step.controlId;

            string stepLabel = string.IsNullOrWhiteSpace(step.controlId)
                ? step.stepType
                : step.controlId;
            SetStepStatus($"Step {i + 1}/{procedure.steps.Count}: {stepLabel}");

            if (!string.IsNullOrWhiteSpace(step.instructionText))
            {
                SetTextResponse(step.instructionText);
                yield return WaitForTtsToFinish();
            }

            bool isControlStep = string.Equals(step.stepType, "control", StringComparison.OrdinalIgnoreCase);
            if (!isControlStep)
            {
                highlighter.ClearCurrentStepHighlight();
                if (nonBlockingStepDelay > 0f)
                {
                    yield return new WaitForSeconds(nonBlockingStepDelay);
                }
                continue;
            }

            if (string.IsNullOrWhiteSpace(step.controlId))
            {
                highlighter.ClearCurrentStepHighlight();
                SetStepStatus($"controlId missing at step {i + 1}");
                mode = RunnerMode.Idle;
                ClearCurrentProcedureState();
                yield break;
            }

            bool highlighted = highlighter.HighlightStepById(step.controlId);
            if (!highlighted)
            {
                highlighter.ClearCurrentStepHighlight();
                SetStepStatus($"Control not found: {step.controlId}");
                Debug.LogWarning($"[MockResponseProcedureRunner] Control not found: {step.controlId}", this);
                mode = RunnerMode.Idle;
                ClearCurrentProcedureState();
                yield break;
            }

            if (step.waitForClick)
            {
                yield return WaitForExpectedClick(step.controlId);

                if (stepWaitTimedOut)
                {
                    mode = RunnerMode.Idle;
                    ClearCurrentProcedureState();
                    yield break;
                }
            }
            else if (nonBlockingStepDelay > 0f)
            {
                yield return new WaitForSeconds(nonBlockingStepDelay);
            }

            highlighter.ClearCurrentStepHighlight();
        }

        highlighter.ClearCurrentStepHighlight();
        SetStepStatus($"Procedure completed: {procedure.displayName}");
        ClearWaitState();
        mode = RunnerMode.Idle;
        ClearCurrentProcedureState();
    }

    private IEnumerator WaitForExpectedClick(string controlId)
    {
        awaitedControlId = controlId;
        awaitedControlClicked = false;
        stepWaitTimedOut = false;

        float startTime = Time.time;
        while (!awaitedControlClicked)
        {
            if (clickWaitTimeoutSeconds > 0f && Time.time - startTime >= clickWaitTimeoutSeconds)
            {
                stepWaitTimedOut = true;
                SetStepStatus($"Timeout waiting for click: {controlId}");
                if (highlighter != null)
                {
                    highlighter.ClearCurrentStepHighlight();
                }
                ClearWaitState();
                yield break;
            }

            yield return null;
        }

        ClearWaitState();
    }

    private List<string> GetDistinctControlIds(List<string> rawIds)
    {
        List<string> result = new List<string>();
        if (rawIds == null)
        {
            return result;
        }

        for (int i = 0; i < rawIds.Count; i++)
        {
            string id = rawIds[i];
            if (string.IsNullOrWhiteSpace(id))
            {
                continue;
            }

            bool exists = false;
            for (int j = 0; j < result.Count; j++)
            {
                if (string.Equals(result[j], id, IdComparison))
                {
                    exists = true;
                    break;
                }
            }

            if (!exists)
            {
                result.Add(id);
            }
        }

        return result;
    }

    private void OnControlActivated(string clickedControlId)
    {
        if (string.IsNullOrWhiteSpace(awaitedControlId) || string.IsNullOrWhiteSpace(clickedControlId))
        {
            return;
        }

        if (string.Equals(awaitedControlId, clickedControlId, IdComparison))
        {
            awaitedControlClicked = true;
        }
    }

    private void ClearWaitState()
    {
        awaitedControlId = null;
        awaitedControlClicked = false;
        stepWaitTimedOut = false;
    }

    private void ClearCurrentProcedureState()
    {
        currentProcedureId = string.Empty;
        currentStepIndex = 0;
        currentStepControlId = string.Empty;
    }

    private void ClearPausedProcedureState()
    {
        hasPausedProcedure = false;
        pausedProcedureId = string.Empty;
        pausedStepIndex = 0;
    }

    private void SetTextResponse(string message)
    {
        if (textResponseLabel != null)
        {
            textResponseLabel.text = message;
        }

        // Speak the response aloud if TTS is available
        if (ttsSpeaker != null && !string.IsNullOrWhiteSpace(message))
        {
            ttsSpeaker.Speak(message);
        }
    }

    /// <summary>
    /// Waits until TTSSpeaker finishes speaking. Yields immediately if no TTS assigned.
    /// </summary>
    private IEnumerator WaitForTtsToFinish()
    {
        if (ttsSpeaker == null) yield break;

        // Give TTS a frame to start
        yield return null;

        while (ttsSpeaker.IsSpeaking)
        {
            yield return null;
        }
    }

    private void SetStepStatus(string message)
    {
        if (stepStatusLabel != null)
        {
            stepStatusLabel.text = message;
        }
    }
}
