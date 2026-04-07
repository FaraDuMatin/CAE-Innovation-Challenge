using UnityEngine;

public class LaunchProcedure : MonoBehaviour
{
    [SerializeField] private MockResponseProcedureRunner runner;

    private void Start()
    {
        if (runner == null)
        {
            runner = FindFirstObjectByType<MockResponseProcedureRunner>();
        }

        if (runner == null)
        {
            Debug.LogError("[LaunchProcedure] MockResponseProcedureRunner not found in scene.", this);
            return;
        }

        runner.RunDefaultResponse();
    }
}
