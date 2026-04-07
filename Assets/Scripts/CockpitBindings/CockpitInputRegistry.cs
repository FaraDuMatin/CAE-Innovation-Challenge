using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(
    fileName = "CockpitInputRegistry",
    menuName = "CAE/B737/Cockpit Input Registry",
    order = 2)]
public class CockpitInputRegistry : ScriptableObject
{
    public List<CockpitInputData> inputs = new();

    public CockpitInputData GetById(string inputId)
    {
        for (int i = 0; i < inputs.Count; i++)
        {
            CockpitInputData item = inputs[i];
            if (item != null && item.inputId == inputId)
            {
                return item;
            }
        }

        return null;
    }

    public CockpitInputData GetByObjectName(string objectName)
    {
        for (int i = 0; i < inputs.Count; i++)
        {
            CockpitInputData item = inputs[i];
            if (item != null && item.targetObjectName == objectName)
            {
                return item;
            }
        }

        return null;
    }
}
