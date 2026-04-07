using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public static class CockpitInputAssetGenerator
{
    private const string RootFolder = "Assets/Data/B737";
    private const string InputsFolder = "Assets/Data/B737/Inputs";
    private const string RegistryPath = "Assets/Data/B737/B737_InputRegistry.asset";

    private static readonly string[] InputNames =
    {
        "ParkingBrake",
        "BatteryMaster",
        "StandbyPower",
        "GroundPower",
        "ApuSelector",
        "ApuGen1",
        "ApuGen2",
        "FuelPumpsGroup1",
        "FuelPumpsGroup2",
        "ApuBleed",
        "HydraulicPumps",
        "ElectricalPumps",
        "Engine1Starter",
        "Engine2Starter",
        "FuelControlLever1",
        "FuelControlLever2",
        "EngineGen1",
        "EngineGen2",
        "SeatbeltSign",
        "ElevatorTrim",
        "FlapsLever",
        "Yoke",
        "AltimeterDial",
        "TransponderDial",
        "LandingLights",
        "TaxiLights",
        "LandingGearLever",
        "ThrustLevers",
        "SpeedBrakeLever"
    };

    [MenuItem("CAE/B737/Generate Input Assets and Registry")]
    public static void GenerateAssetsAndRegistry()
    {
        EnsureFolder("Assets", "Data");
        EnsureFolder("Assets/Data", "B737");
        EnsureFolder(RootFolder, "Inputs");

        List<CockpitInputData> createdOrFound = new();

        for (int i = 0; i < InputNames.Length; i++)
        {
            string inputName = InputNames[i];
            string assetPath = $"{InputsFolder}/{inputName}.asset";

            CockpitInputData data = AssetDatabase.LoadAssetAtPath<CockpitInputData>(assetPath);
            if (data == null)
            {
                data = ScriptableObject.CreateInstance<CockpitInputData>();
                AssetDatabase.CreateAsset(data, assetPath);
            }

            data.inputId = inputName;
            data.displayName = Nicify(inputName);
            data.targetObjectName = inputName;
            data.minValue = 0f;
            data.maxValue = 1f;
            data.currentValue = Mathf.Clamp(data.currentValue, data.minValue, data.maxValue);
            if (string.IsNullOrWhiteSpace(data.aiDescription))
            {
                data.aiDescription = $"Controls {Nicify(inputName).ToLowerInvariant()}.";
            }

            EditorUtility.SetDirty(data);
            createdOrFound.Add(data);
        }

        CockpitInputRegistry registry = AssetDatabase.LoadAssetAtPath<CockpitInputRegistry>(RegistryPath);
        if (registry == null)
        {
            registry = ScriptableObject.CreateInstance<CockpitInputRegistry>();
            AssetDatabase.CreateAsset(registry, RegistryPath);
        }

        registry.inputs = createdOrFound;
        EditorUtility.SetDirty(registry);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Selection.activeObject = registry;
        Debug.Log($"Generated {createdOrFound.Count} cockpit input assets and synced registry at {RegistryPath}");
    }

    private static void EnsureFolder(string parent, string child)
    {
        string full = $"{parent}/{child}";
        if (!AssetDatabase.IsValidFolder(full))
        {
            AssetDatabase.CreateFolder(parent, child);
        }
    }

    private static string Nicify(string raw)
    {
        return ObjectNames.NicifyVariableName(raw);
    }
}
