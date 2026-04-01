using System.Reflection;
using PEAK_Visuals.Configuration;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace PEAK_Visuals;

public class Settings
{
    private ConfigurationHandler _configurationHandler = Plugin.Instance.ConfigurationHandler;
    
    public void SetAllSettings()
    {
        SetResolutionScale();
        SetUpscaler();
        SetLODQuality();
        SetShadowDistance();
        SetShadowCascades();
        SetAnisotropicFiltering();
        SetSoftShadows();
        SetShadowmapResolution();
    }

    public void SetAllCameraSettings()
    {
        SetPostProcessAA();
        SetMSAA();
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
        if (MainCamera.instance.cam.TryGetComponent(out UniversalAdditionalCameraData data))
        {
            data.antialiasing = (AntialiasingMode)Plugin.Instance.ConfigurationHandler.CameraAA;
            if (Plugin.Instance.ConfigurationHandler.MSAA != 0 && Plugin.Instance.ConfigurationHandler.CameraAA == 3)
            {
                Plugin.Instance.ConfigurationHandler.ConfigMSAA.Value = 2; //TAA has been deliberately turned on, so turn off MSAA
            }
        }
        Plugin.Log.LogInfo("Camera AA applied: " + _configurationHandler.CameraAA);
    }

    public void SetMSAA()
    {
        if (GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset pipeline)
        {
            pipeline.msaaSampleCount = Plugin.Instance.ConfigurationHandler.MSAA;
            if (Plugin.Instance.ConfigurationHandler.MSAA != 0 && Plugin.Instance.ConfigurationHandler.CameraAA == 3)
            {
                Plugin.Instance.ConfigurationHandler.ConfigCameraAA.Value = 2; //TAA cannot be active while MSAA is, default to SMAA.
            }
        }
        Plugin.Log.LogInfo("MSAA applied: " + _configurationHandler.MSAA);
    }

    public void SetResolutionScale()
    {
        if (!(GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset currentRenderPipeline))
            return;
        currentRenderPipeline.renderScale = _configurationHandler.RenderScale;
        Plugin.Log.LogInfo("Render Scale applied: " + _configurationHandler.RenderScale);
    }

    public void SetUpscaler()
    {
        if (!(GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset currentRenderPipeline))
            return;
        currentRenderPipeline.upscalingFilter = (UpscalingFilterSelection) _configurationHandler.UpscalingFilter;
        Plugin.Log.LogInfo("Upscaling Filter applied: " + _configurationHandler.UpscalingFilter);
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