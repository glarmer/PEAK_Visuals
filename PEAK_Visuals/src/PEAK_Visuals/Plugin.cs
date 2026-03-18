using System.Collections.Generic;
using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using PEAK_Visuals.Patches;
using PEAK_Visuals.Configuration;
using Photon.Pun;
using UnityEngine;

namespace PEAK_Visuals;

[BepInAutoPlugin]
public partial class Plugin : BaseUnityPlugin
{
    internal static ManualLogSource Log { get; private set; } = null!;
    public static Plugin Instance {get; private set;} = null!;
    public ConfigurationHandler ConfigurationHandler {get; private set;} = null!;
    public Settings Settings { get; private set; } = null!;
    private readonly Harmony _harmony = new(Id);
    
    private ModConfigurationUI _ui;

    private void Awake()
    {
        Log = Logger;
        if (Instance == null)
        {
            Instance = this;
        }

        ConfigurationHandler = new ConfigurationHandler(Config);
        Settings = new Settings();
        
        _harmony.PatchAll();
        
        var go = new GameObject("PEAKScaledUI");
        DontDestroyOnLoad(go);
        _ui = go.AddComponent<ModConfigurationUI>();
        _ui.Init([
            Option.Float("Render Scale", ConfigurationHandler.ConfigRenderScale, 0.1f, 2f, 0.1f),
            Option.Int(
                "Upscaling Filter",
                ConfigurationHandler.ConfigUpscalingFilter, 0, 4,
                displayValue: () => ConfigurationHandler.ConfigUpscalingFilter.Value switch
                {
                    0 => "Auto",
                    1 => "Linear",
                    2 => "Nearest Neighbor",
                    3 => "FSR 1.0",
                    4 => "STP",
                    _ => "???"
                }
            ),
            Option.Bool("Anisotropic Filtering", ConfigurationHandler.ConfigAnisotropicFiltering),
            Option.Float("LOD Quality", ConfigurationHandler.ConfigLODQuality, 0.1f, 10f, 0.1f),
            Option.Int("Shadow Distance", ConfigurationHandler.ConfigShadowDistance, 0, 1000, 25),
            Option.Int("Shadow Cascades", ConfigurationHandler.ConfigShadowCascades, 1, 10),
            Option.Int(
                "Camera Antialiasing",
                ConfigurationHandler.ConfigCameraAA, 0, 3,
                displayValue: () => ConfigurationHandler.ConfigCameraAA.Value switch
                {
                    0 => "None",
                    1 => "FXAA",
                    2 => "SMAA",
                    3 => "TAA",
                    _ => "???"
                }
            ),
            Option.Int(
                "MSAA",
                ConfigurationHandler.ConfigMSAA, 0, 8, 2,
                displayValue: () => ConfigurationHandler.ConfigMSAA.Value switch
                {
                    0 => "Off",
                    2 => "2x",
                    4 => "4x",
                    8 => "8x",
                    _ => ConfigurationHandler.ConfigMSAA.Value + "x"
                }
            ),
            Option.InputAction("Menu Key", ConfigurationHandler.ConfigMenuKey)
        ]);
        
        Log.LogInfo($"Plugin {Name} is loaded!");
    }
}