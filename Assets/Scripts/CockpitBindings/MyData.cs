using UnityEngine;

[CreateAssetMenu(
	fileName = "CockpitInput",
	menuName = "CAE/B737/Cockpit Input",
	order = 1)]
public class CockpitInputData : ScriptableObject
{
	[Header("Identity")]
	public string inputId;
	public string displayName;

	[Header("Value")]
	public float currentValue;
	public float minValue = 0f;
	public float maxValue = 1f;

	[Header("AI Context")]
	[TextArea(2, 5)]
	public string aiDescription;

	[Header("Optional Mapping Hint")]
	public string targetObjectName;

	public void SetValueClamped(float newValue)
	{
		currentValue = Mathf.Clamp(newValue, minValue, maxValue);
	}
}
