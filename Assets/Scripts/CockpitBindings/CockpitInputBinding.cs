using UnityEngine;

public class CockpitInputBinding : MonoBehaviour
{
    [SerializeField] private CockpitInputData inputData;

    public CockpitInputData InputData => inputData;

    public void SetNormalizedValue(float value)
    {
        if (inputData == null)
        {
            return;
        }

        inputData.SetValueClamped(value);
    }

    [ContextMenu("Assign Target Name From GameObject")]
    private void AssignTargetNameFromGameObject()
    {
        if (inputData != null)
        {
            inputData.targetObjectName = gameObject.name;
        }
    }
}
