using System;
using BepInEx.Configuration;
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
    
    public ConfigEntry<int> ConfigCameraAA;
    public ConfigEntry<int> ConfigMSAA;
    
    public float RenderScale => ConfigRenderScale.Value;
    public int UpscalingFilter => ConfigUpscalingFilter.Value;
    public float LodQuality => ConfigLODQuality.Value;
    public int ShadowDistance => ConfigShadowDistance.Value;
    public int ShadowCascades => ConfigShadowCascades.Value;
    public int CameraAA => ConfigCameraAA.Value;
    public int MSAA => ConfigMSAA.Value;
    
    public ConfigurationHandler(ConfigFile configFile)
    {
        _config = configFile;
        
        Plugin.Log.LogInfo("ConfigurationHandler initialising");
        
        
        ConfigRenderScale = _config.Bind
        (
            "Scaling",
            "RenderScale",
            1f,
            "Controls the render scale of the game. Native is 1.0 (100%). Range 0.1-2.0"
        );
        Plugin.Log.LogInfo("ConfigurationHandler: Render Scale set to: " + ConfigRenderScale.Value);
        ConfigRenderScale.SettingChanged += OnRenderScaleChanged;
        if (ConfigRenderScale.Value < 0.1f)
        {
            ConfigRenderScale.Value = 0.1f;
        }
        else if (ConfigRenderScale.Value > 2f)
        {
            ConfigRenderScale.Value = 2f;
        }
        
        ConfigUpscalingFilter = _config.Bind
        (
            "Scaling",
            "UpscalingFilter",
            1,
            "Controls what filter the game uses to scale to your monitor resolution. 0 = auto, 1 = linear, 2 = point, 3 = FSR 1.0, 4 = STP"
        );
        Plugin.Log.LogInfo("ConfigurationHandler: Upscaling filter set to: " + ConfigUpscalingFilter.Value);
        ConfigUpscalingFilter.SettingChanged += OnUpscalingFilterChanged;
        if (ConfigUpscalingFilter.Value < 0)
        {
            ConfigUpscalingFilter.Value = 0;
        }
        else if (ConfigUpscalingFilter.Value > 4)
        {
            ConfigUpscalingFilter.Value = 4;
        }
        
        ConfigLODQuality = _config.Bind
        (
            "LOD",
            "LODQuality",
            2.5f,
            "Controls the LOD bias of the game. PEAK's High equates to 1.0. Higher values increase detail distance. Range 0.1-10"
        );

        Plugin.Log.LogInfo("ConfigurationHandler: LOD Quality set to: " + ConfigLODQuality.Value);

        ConfigLODQuality.SettingChanged += OnLODQualityChanged;

        if (ConfigLODQuality.Value < 0.1f)
        {
            ConfigLODQuality.Value = 0.1f;
        }
        else if (ConfigLODQuality.Value > 10f)
        {
            ConfigLODQuality.Value = 10f;
        }
        
        ConfigShadowDistance = _config.Bind
        (
            "Shadows",
            "ShadowDistance",
            200,
            "Controls the maximum distance shadows are rendered. PEAK's High option equates to 200. Higher values improve distant shadows but reduce performance. Range 0-1000"
        );
        Plugin.Log.LogInfo("ConfigurationHandler: Shadow Distance set to: " + ConfigShadowDistance.Value);
        ConfigShadowDistance.SettingChanged += OnShadowDistanceChanged;
        if (ConfigShadowDistance.Value < 0)
        {
            ConfigShadowDistance.Value = 0;
        }
        else if (ConfigShadowDistance.Value > 1000)
        {
            ConfigShadowDistance.Value = 1000;
        }
        
        ConfigShadowCascades = _config.Bind
        (
            "Shadows",
            "ShadowCascades",
            4,
            "Controls the number of shadow cascades used by the directional light. PEAK's High option equates to 2. Higher values improve shadow stability. Range 1-10"
        );
        Plugin.Log.LogInfo("ConfigurationHandler: Shadow Cascades set to: " + ConfigShadowCascades.Value);
        ConfigShadowCascades.SettingChanged += OnShadowCascadesChanged;
        if (ConfigShadowCascades.Value < 1)
        {
            ConfigShadowCascades.Value = 1;
        }
        else if (ConfigShadowCascades.Value > 10)
        {
            ConfigShadowCascades.Value = 10;
        }
        
        ConfigCameraAA = _config.Bind
        (
            "AntiAliasing",
            "CameraAA",
            2,
            "Controls what type of AA the Camera uses. By default PEAK uses Temporal Antialiasing (TAA). All of these options are essentially clever blur filters and sometimes people find them unpleasant. 0 = None, 1 = FXAA, 2 = SMAA, 3 = TAA."
        );
        Plugin.Log.LogInfo("ConfigurationHandler: Camera AA set to: " + ConfigCameraAA.Value);
        ConfigCameraAA.SettingChanged += OnCameraAAChanged;
        if (ConfigShadowCascades.Value < 0)
        {
            ConfigShadowCascades.Value = 0;
        }
        else if (ConfigShadowCascades.Value > 3)
        {
            ConfigShadowCascades.Value = 3;
        }
        
        ConfigMSAA = _config.Bind
        (
            "AntiAliasing",
            "MSAA",
            8,
            "Controls whether Multi-Sample Anti-Aliasing (MSAA) is enabled. MSAA smooths jagged edges on geometry by sampling pixels multiple times. It is generally sharper than post-process AA methods but only affects object edges and can slightly reduce performance. PEAK does not use it by default. Higher values = better quality but worse performance. Valid values = 0, 2, 4, 8."
        );
        Plugin.Log.LogInfo("ConfigurationHandler: MSAA set to: " + ConfigMSAA.Value);
        ConfigMSAA.SettingChanged += OnMSAAChanged;
        if (ConfigMSAA.Value < 0)
        {
            ConfigMSAA.Value = 0;
        }
        else if (ConfigMSAA.Value > 8)
        {
            ConfigMSAA.Value = 8;
        }
        
        ConfigMenuKey = _config.Bind
        (
            "General",
            "Config Menu Key",
            "<Keyboard>/f11",
            "Control path for opening the mod configuration menu (e.g. <Keyboard>/f2, <Keyboard>/space, <Keyboard>/escape)"
        );
        Plugin.Log.LogInfo("ConfigurationHandler: Config Menu Key: " + ConfigMenuKey.Value);
        SetupInputAction();
        ConfigMenuKey.SettingChanged += OnMenuKeyChanged;
        
        Plugin.Log.LogInfo("ConfigurationHandler initialised");
    }

    private void OnCameraAAChanged(object sender, EventArgs e)
    {
        Plugin.Instance.Settings.SetPostProcessAA();
    }

    private void OnMSAAChanged(object sender, EventArgs e)
    {
        Plugin.Instance.Settings.SetMSAA();
    }

    private void OnRenderScaleChanged(object sender, EventArgs e)
    {
        Plugin.Instance.Settings.SetResolutionScale();
    }
    
    private void OnUpscalingFilterChanged(object sender, EventArgs e)
    {
        Plugin.Instance.Settings.SetUpscaler();
    }
    
    private void OnLODQualityChanged(object sender, EventArgs e)
    {
        Plugin.Instance.Settings.SetLODQuality();
    }
    
    private void OnShadowDistanceChanged(object sender, EventArgs e)
    {
        Plugin.Instance.Settings.SetShadowDistance();
    }

    private void OnShadowCascadesChanged(object sender, EventArgs e)
    {
        Plugin.Instance.Settings.SetShadowCascades();
    }
    
    private void OnMenuKeyChanged(object sender, EventArgs e)
    {
        SetupInputAction();
    }
    
    private void SetupInputAction()
    {
        MenuAction?.Dispose();

        MenuAction = new InputAction(type: InputActionType.Button);
        MenuAction.AddBinding(ConfigMenuKey.Value);
        MenuAction.Enable();
    }
}