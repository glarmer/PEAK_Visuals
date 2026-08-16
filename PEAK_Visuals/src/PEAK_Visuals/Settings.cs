using System.Reflection;
using PEAK_Visuals.Configuration;
using PEAK_Visuals.Upscaling;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace PEAK_Visuals;

public class Settings
{
    private ConfigurationHandler _configurationHandler = Plugin.Instance.ConfigurationHandler;

    internal static float GetDLSSRenderScale(DLSSMode mode)
    {
        return mode switch
        {
            DLSSMode.UltraQuality => 0.77f,
            DLSSMode.Quality => 0.67f,
            DLSSMode.Balanced => 0.58f,
            DLSSMode.Performance => 0.5f,
            DLSSMode.UltraPerformance => 1f / 3f,
            DLSSMode.DLAA => 1f,
            _ => 1f
        };
    }
    
    public void SetAllSettings()
    {
        SetDLSS();
        SetLODQuality();
        SetShadowDistance();
        SetShadowCascades();
        SetAnisotropicFiltering();
        SetSoftShadows();
        SetShadowmapResolution();
    }

    public void SetAllCameraSettings()
    {
        SetDLSS();
    }

    public void SetSoftShadows()
    {
        if (GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset pipeline)
        {
            var flags = BindingFlags.NonPublic | BindingFlags.Instance;
            var softShadowsField = pipeline.GetType().GetField("m_SoftShadowsSupported", flags);
            softShadowsField.SetValue(pipeline, _configurationHandler.SoftShadows);
            Plugin.Log.LogInfo("Soft Shadows applied: " + _configurationHandler.ShadowmapResolution);
        }
    }

    public void SetShadowmapResolution()
    {
        if (GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset pipeline)
        {
            pipeline.mainLightShadowmapResolution = _configurationHandler.ShadowmapResolution;
            Plugin.Log.LogInfo("Shadowmap Resolution applied: " + _configurationHandler.ShadowmapResolution);
        }
    }

    public void SetAnisotropicFiltering()
    {
        QualitySettings.anisotropicFiltering = _configurationHandler.AnisotropicFiltering ? AnisotropicFiltering.ForceEnable : AnisotropicFiltering.Disable;
        Plugin.Log.LogInfo("Anisotropic Filtering applied: " + _configurationHandler.AnisotropicFiltering);
    }

    public void SetPostProcessAA()
    {
        if (MainCamera.instance != null &&
            MainCamera.instance.cam != null &&
            MainCamera.instance.cam.TryGetComponent(out UniversalAdditionalCameraData data))
        {
            data.antialiasing = Plugin.Instance.ConfigurationHandler.DLSSEnabled
                ? AntialiasingMode.None
                : (AntialiasingMode)Plugin.Instance.ConfigurationHandler.CameraAA;
            if (Plugin.Instance.ConfigurationHandler.MSAA != 0 && Plugin.Instance.ConfigurationHandler.CameraAA == 3)
            {
                Plugin.Instance.ConfigurationHandler.ConfigMSAA.Value = 0;
            }
        }
        Plugin.Log.LogInfo("Camera AA applied: " + _configurationHandler.CameraAA);
    }

    public void SetMSAA()
    {
        if (GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset pipeline)
        {
            pipeline.msaaSampleCount = Plugin.Instance.ConfigurationHandler.DLSSEnabled
                ? 1
                : Plugin.Instance.ConfigurationHandler.MSAA;
            if (Plugin.Instance.ConfigurationHandler.MSAA != 0 && Plugin.Instance.ConfigurationHandler.CameraAA == 3)
            {
                Plugin.Instance.ConfigurationHandler.ConfigCameraAA.Value = 2;
            }
        }
        Plugin.Log.LogInfo("MSAA applied: " + _configurationHandler.MSAA);
    }

    public void SetResolutionScale()
    {
        if (!(GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset currentRenderPipeline))
            return;
        float renderScale = _configurationHandler.DLSSEnabled
            ? GetDLSSRenderScale(_configurationHandler.DLSSMode)
            : _configurationHandler.RenderScale;
        currentRenderPipeline.renderScale = renderScale;
        Plugin.Log.LogInfo(_configurationHandler.DLSSEnabled
            ? $"Render Scale applied: {renderScale} (DLSS {_configurationHandler.DLSSMode})"
            : "Render Scale applied: " + renderScale);
    }

    public void SetUpscaler()
    {
        if (!(GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset currentRenderPipeline))
            return;
        currentRenderPipeline.upscalingFilter = _configurationHandler.DLSSEnabled
            ? UpscalingFilterSelection.Linear
            : (UpscalingFilterSelection) _configurationHandler.UpscalingFilter;
        Plugin.Log.LogInfo("Upscaling Filter applied: " + currentRenderPipeline.upscalingFilter);
    }

    public void SetDLSS()
    {
        SetResolutionScale();
        SetUpscaler();
        SetPostProcessAA();
        SetMSAA();
        Plugin.Instance.DLSSController?.ResetHistory();
        Plugin.Log.LogInfo("DLSS mode applied: " + _configurationHandler.DLSSMode);
    }

    public void SetLODQuality()
    {
        QualitySettings.lodBias = _configurationHandler.LodQuality;
        Plugin.Log.LogInfo("LOD Bias applied: " + _configurationHandler.LodQuality);
    }

    public void SetShadowDistance()
    {
        if (GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset pipeline)
        {
            pipeline.shadowDistance = _configurationHandler.ShadowDistance;
            Plugin.Log.LogInfo("Shadow Distance applied: " + _configurationHandler.ShadowDistance);
        }
    }

    public void SetShadowCascades()
    {
        if (GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset pipeline)
        {
            pipeline.shadowCascadeCount = _configurationHandler.ShadowCascades;
            Plugin.Log.LogInfo("Shadow Cascades applied: " + _configurationHandler.ShadowCascades);
        }
    }
    
}
