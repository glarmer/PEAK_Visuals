using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using System.Collections.Generic;
using PEAK_Visuals.Configuration;
using PEAK_Visuals.Upscaling;
using UnityEngine;

namespace PEAK_Visuals;

[BepInAutoPlugin]
[BepInDependency(UpscalerLibIntegration.PluginGuid, BepInDependency.DependencyFlags.SoftDependency)]
public partial class Plugin : BaseUnityPlugin
{
    internal static ManualLogSource Log { get; private set; } = null!;
    public static Plugin Instance {get; private set;} = null!;
    public ConfigurationHandler ConfigurationHandler {get; private set;} = null!;
    public Settings Settings { get; private set; } = null!;
    internal DLSSController? DLSSController { get; private set; }
    internal bool UpscalerLibAvailable { get; private set; }
    private readonly Harmony _harmony = new(Id);
    
    private ModConfigurationUI _ui;

    private void Awake()
    {
        Log = Logger;
        if (Instance == null)
        {
            Instance = this;
        }

        UpscalerLibAvailable = UpscalerLibIntegration.IsInstalled;
        if (!UpscalerLibAvailable)
            Log.LogInfo("UpscalerLib is not installed. DLSS options will be hidden and DLSS will remain off.");
        
        var go = new GameObject("PEAKVisualsUI");
        DontDestroyOnLoad(go);
        if (UpscalerLibAvailable)
            DLSSController = go.AddComponent<DLSSController>();
        _ui = go.AddComponent<ModConfigurationUI>();

        ConfigurationHandler = new ConfigurationHandler(Config);
        Settings = new Settings();
        if (!UpscalerLibAvailable)
            ConfigurationHandler.ForceDLSSOff();

        _harmony.PatchAll();
        List<Option> options =
        [
            Option.Float(
                "Render Scale",
                ConfigurationHandler.ConfigRenderScale,
                0.1f,
                2f,
                0.1f,
                () => ConfigurationHandler.DLSSEnabled,
                () => ConfigurationHandler.DLSSEnabled
                    ? $"{PEAK_Visuals.Settings.GetDLSSRenderScale(ConfigurationHandler.DLSSMode):F2} ({ConfigurationHandler.DLSSMode})"
                    : ConfigurationHandler.ConfigRenderScale.Value.ToString("F3")
            ),
            Option.Int(
                "Upscaling Filter",
                ConfigurationHandler.ConfigUpscalingFilter, 0, 4,
                isDisabled: () => ConfigurationHandler.DLSSEnabled,
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
        ];

        if (UpscalerLibAvailable)
        {
            options.AddRange(
            [
                Option.Int(
                    "DLSS",
                    ConfigurationHandler.ConfigDLSSMode, 0, 6,
                    displayValue: () => ConfigurationHandler.DLSSMode switch
                    {
                        DLSSMode.Off => "Off",
                        DLSSMode.UltraQuality => "Quality (77%)",
                        DLSSMode.Quality => "Quality (67%)",
                        DLSSMode.Balanced => "Balanced (58%)",
                        DLSSMode.Performance => "Performance (50%)",
                        DLSSMode.UltraPerformance => "Ultra Performance (33%)",
                        DLSSMode.DLAA => "DLAA (100%)",
                        _ => "???"
                    }
                ),
                Option.Int(
                    "DLSS Preset",
                    ConfigurationHandler.ConfigDLSSPreset,
                    (int)DLSSPresetMode.PresetF,
                    (int)DLSSPresetMode.PresetM,
                    isDisabled: () => !ConfigurationHandler.DLSSEnabled,
                    displayValue: () => ConfigurationHandler.DLSSPresetMode switch
                    {
                        DLSSPresetMode.PresetF => "Preset F (CNN)",
                        DLSSPresetMode.PresetJ => "Preset J (Transformer)",
                        DLSSPresetMode.PresetK => "Preset K (Transformer)",
                        DLSSPresetMode.PresetL => "Preset L (Transformer DLSS 4.5)",
                        DLSSPresetMode.PresetM => "Preset M (Transformer DLSS 4.5)",
                        _ => "???"
                    }
                ),
                Option.Bool(
                    "DLSS Jitter",
                    ConfigurationHandler.ConfigDLSSJitter,
                    () => !ConfigurationHandler.DLSSEnabled
                ),
                Option.Float(
                    "DLSS Jitter Strength",
                    ConfigurationHandler.ConfigDLSSJitterStrength,
                    0f,
                    1f,
                    0.05f,
                    () => !ConfigurationHandler.DLSSEnabled || !ConfigurationHandler.DLSSJitterEnabled,
                    () => $"{ConfigurationHandler.DLSSJitterStrength * 100f:F0}%"
                )
            ]);
        }

        options.AddRange(
        [
            Option.Bool("Anisotropic Filtering", ConfigurationHandler.ConfigAnisotropicFiltering),
            Option.Float("LOD Quality", ConfigurationHandler.ConfigLODQuality, 0.1f, 10f, 0.1f),
            Option.Int("Shadowmap Resolution", ConfigurationHandler.ConfigShadowmapResolution, 0, 10240, 1024),
            Option.Int("Shadow Distance", ConfigurationHandler.ConfigShadowDistance, 0, 1000, 25),
            Option.Int("Shadow Cascades", ConfigurationHandler.ConfigShadowCascades, 1, 10),
            Option.Bool("Soft Shadows", ConfigurationHandler.ConfigSoftShadows),
            Option.Int(
                "Camera Antialiasing",
                ConfigurationHandler.ConfigCameraAA, 0, 3,
                isDisabled: () => ConfigurationHandler.DLSSEnabled,
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
                isDisabled: () => ConfigurationHandler.DLSSEnabled,
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

        _ui.Init(options);
        
        Log.LogInfo($"Plugin {Name} is loaded!");
    }
}
