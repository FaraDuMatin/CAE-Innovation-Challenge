using TMPro;
using UnityEngine;

public class MockStepClickSimulator : MonoBehaviour
{
    [SerializeField] private MockResponseProcedureRunner runner;
    [SerializeField] private TMP_Text statusLabel;

    private void Reset()
    {
        if (runner == null)
        {
            runner = FindFirstObjectByType<MockResponseProcedureRunner>();
        }
    }

    public void SimulateClick()
    {
        if (runner == null)
        {
            SetStatus("Runner missing");
            return;
        }

        if (!runner.IsWaitingForClick)
        {
            SetStatus("Runner is not waiting for a click");
            return;
        }

        string awaited = runner.AwaitedControlId;
        runner.SimulateExpectedClick();
        SetStatus("Simulated click for: " + awaited);
    }

    private void SetStatus(string message)
    {
        if (statusLabel != null)
        {
            statusLabel.text = message;
        }
    }
}
