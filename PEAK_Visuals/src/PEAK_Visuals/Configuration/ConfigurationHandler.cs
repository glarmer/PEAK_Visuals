using System;
using BepInEx.Configuration;
using UnityEngine;
using UnityEngine.InputSystem;

namespace PEAK_Visuals.Configuration;

public class ConfigurationHandler
{
    private ConfigFile _config;
    public InputAction MenuAction { get; set; }

    public ConfigEntry<float> ConfigRenderScale;
    public ConfigEntry<int> ConfigUpscalingFilter;
    public ConfigEntry<float> ConfigLODQuality;
    public ConfigEntry<string> ConfigMenuKey;
    public ConfigEntry<int> ConfigShadowDistance;
    public ConfigEntry<int> ConfigShadowCascades;
    public ConfigEntry<int> ConfigShadowmapResolution;
    public ConfigEntry<bool> ConfigSoftShadows;
    public ConfigEntry<bool> ConfigAnisotropicFiltering;
    public ConfigEntry<int> ConfigCameraAA;
    public ConfigEntry<int> ConfigMSAA;

    public float RenderScale => ConfigRenderScale.Value;
    public int UpscalingFilter => ConfigUpscalingFilter.Value;
    public float LodQuality => ConfigLODQuality.Value;
    public int ShadowDistance => ConfigShadowDistance.Value;
    public int ShadowCascades => ConfigShadowCascades.Value;
    public int ShadowmapResolution => ConfigShadowmapResolution.Value;
    public bool SoftShadows => ConfigSoftShadows.Value;
    public bool AnisotropicFiltering => ConfigAnisotropicFiltering.Value;
    public int CameraAA => ConfigCameraAA.Value;
    public int MSAA => ConfigMSAA.Value;

    public ConfigurationHandler(ConfigFile configFile)
    {
        _config = configFile;

        Plugin.Log.LogInfo("ConfigurationHandler initialising");

        ConfigRenderScale = Bind(
            "Scaling",
            "RenderScale",
            1f,
            "Controls the render scale of the game. Native is 1.0 (100%). Range 0.1-2.0",
            () => Plugin.Instance.Settings.SetResolutionScale(),
            v => Mathf.Clamp(v, 0.1f, 2f)
        );

        ConfigUpscalingFilter = Bind(
            "Scaling",
            "UpscalingFilter",
            1,
            "Controls what filter the game uses to scale to your monitor resolution. 0 = auto, 1 = linear, 2 = point, 3 = FSR 1.0, 4 = STP",
            () => Plugin.Instance.Settings.SetUpscaler(),
            v => Mathf.Clamp(v, 0, 4)
        );

        ConfigLODQuality = Bind(
            "LOD",
            "LODQuality",
            2.5f,
            "Controls the LOD bias of the game. PEAK's High equates to 1.0. Higher values increase detail distance. Range 0.1-10",
            () => Plugin.Instance.Settings.SetLODQuality(),
            v => Mathf.Clamp(v, 0.1f, 10f)
        );

        ConfigShadowDistance = Bind(
            "Shadows",
            "ShadowDistance",
            300,
            "Controls the maximum distance shadows are rendered. PEAK's High option equates to 200. Higher values improve distant shadows but reduce performance. Range 0-1000",
            () => Plugin.Instance.Settings.SetShadowDistance(),
            v => Mathf.Clamp(v, 0, 1000)
        );

        ConfigShadowCascades = Bind(
            "Shadows",
            "ShadowCascades",
            4,
            "Controls the number of shadow cascades used by the directional light. PEAK's High option equates to 2. Higher values improve shadow stability. Range 1-10",
            () => Plugin.Instance.Settings.SetShadowCascades(),
            v => Mathf.Clamp(v, 1, 4)
        );

        ConfigShadowmapResolution = Bind(
            "Shadows",
            "ShadowmapResolution",
            4096,
            "Controls the quality of the shadows, can reduce performance so turn it down if you're having issues. Makes the trees less wobbly",
            () => Plugin.Instance.Settings.SetShadowmapResolution(),
            v => Mathf.Clamp(v, 1024, 20480)
        );

        ConfigSoftShadows = Bind(
            "Shadows",
            "SoftShadows",
            true,
            "Allows shadows to be soft, if your PC is too low spec for a high shadowmap setting this to false can stop the wobblyness of the shadows (but will result in a pixelly effect if the shadowmap res is low)",
            () => Plugin.Instance.Settings.SetSoftShadows()
        );

        ConfigAnisotropicFiltering = Bind(
            "Quality",
            "AnisotropicFiltering",
            true,
            "Helps texture sharpness at angles (set to on)",
            () => Plugin.Instance.Settings.SetAnisotropicFiltering()
        );

        ConfigCameraAA = Bind(
            "AntiAliasing",
            "CameraAA",
            2,
            "Controls what type of AA the Camera uses. By default PEAK uses Temporal Antialiasing (TAA). All of these options are essentially clever blur filters and sometimes people find them unpleasant. 0 = None, 1 = FXAA, 2 = SMAA, 3 = TAA.",
            () => Plugin.Instance.Settings.SetPostProcessAA(),
            v => Mathf.Clamp(v, 0, 3)
        );

        ConfigMSAA = Bind(
            "AntiAliasing",
            "MSAA",
            8,
            "Controls whether Multi-Sample Anti-Aliasing (MSAA) is enabled. MSAA smooths jagged edges on geometry by sampling pixels multiple times. It is generally sharper than post-process AA methods but only affects object edges and can slightly reduce performance. PEAK does not use it by default. Higher values = better quality but worse performance. Valid values = 0, 2, 4, 8.",
            () => Plugin.Instance.Settings.SetMSAA(),
            v => Mathf.Clamp(v, 0, 8)
        );

        ConfigMenuKey = Bind(
            "General",
            "Config Menu Key",
            "<Keyboard>/f11",
            "Control path for opening the mod configuration menu (e.g. <Keyboard>/f2, <Keyboard>/space, <Keyboard>/escape)",
            SetupInputAction
        );

        SetupInputAction();

        Plugin.Log.LogInfo("ConfigurationHandler initialised");
    }

    private ConfigEntry<T> Bind<T>(string section, string key, T defaultValue, string description, Action onChanged = null, Func<T, T> clamp = null)
    {
        var entry = _config.Bind(section, key, defaultValue, description);

        if (clamp != null) 
            entry.Value = clamp(entry.Value);

        Plugin.Log.LogInfo($"{key} set to: {entry.Value}");

        if (onChanged != null)
        {
            entry.SettingChanged += (_, _) =>
            {
                if (clamp != null)
                    entry.Value = clamp(entry.Value);

                onChanged();
            };
        }

        return entry;
    }

    private void SetupInputAction()
    {
        MenuAction?.Dispose();

        MenuAction = new InputAction(type: InputActionType.Button);
        MenuAction.AddBinding(ConfigMenuKey.Value);
        MenuAction.Enable();
    }
}