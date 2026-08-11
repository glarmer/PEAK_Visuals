using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace PEAK_Visuals.Upscaling;

internal sealed class DLSSController : MonoBehaviour
{
    private PeakDLSSRendererFeature? _feature;
    private ScriptableRenderer? _renderer;
    private Camera? _camera;
    private int _jitterIndex;
    private Matrix4x4 _savedProjectionMatrix;
    private Camera? _jitteredCamera;
    private bool _jitterApplied;
    private static readonly float[] HaltonX = GenerateHalton(2, 32);
    private static readonly float[] HaltonY = GenerateHalton(3, 32);
    private static readonly System.Reflection.FieldInfo RendererFeaturesField =
        AccessTools.Field(typeof(ScriptableRenderer), "m_RendererFeatures");

    public void Refresh(Camera? camera = null)
    {
        if (camera != null)
            _camera = camera;
        else if (_camera == null && MainCamera.instance != null)
            _camera = MainCamera.instance.cam;

        ApplyCameraRequirements();
        EnsureFeatureInstalled();
        _feature?.SetTargetCamera(_camera);
        _feature?.SetDLSSActive(Plugin.Instance.ConfigurationHandler.DLSSEnabled);
    }

    public void ResetHistory()
    {
        _jitterIndex = 0;
        _feature?.ResetHistory();
        Refresh();
    }

    private void OnEnable()
    {
        RenderPipelineManager.beginCameraRendering += OnBeginCameraRendering;
        RenderPipelineManager.endCameraRendering += OnEndCameraRendering;
    }

    private void OnDisable()
    {
        RenderPipelineManager.beginCameraRendering -= OnBeginCameraRendering;
        RenderPipelineManager.endCameraRendering -= OnEndCameraRendering;
        RestoreJitteredProjection();
    }

    private void ApplyCameraRequirements()
    {
        if (_camera == null || !Plugin.Instance.ConfigurationHandler.DLSSEnabled)
            return;

        _camera.depthTextureMode |= DepthTextureMode.Depth | DepthTextureMode.MotionVectors;
        if (_camera.TryGetComponent(out UniversalAdditionalCameraData cameraData))
        {
            cameraData.requiresDepthTexture = true;
            cameraData.requiresColorTexture = true;
            cameraData.resetHistory = true;
        }
    }

    private void EnsureFeatureInstalled()
    {
        if (_camera == null || !_camera.TryGetComponent(out UniversalAdditionalCameraData cameraData))
            return;

        var renderer = cameraData.scriptableRenderer;
        if (renderer == null)
            return;

        if (_feature == null)
        {
            _feature = ScriptableObject.CreateInstance<PeakDLSSRendererFeature>();
            _feature.name = "PEAK Visuals DLSS";
            _feature.Create();
            _feature.SetActive(false);
        }

        var features = GetRendererFeatures(renderer);
        if (features == null)
            return;

        if (_renderer == renderer && features.Contains(_feature))
            return;

        if (_renderer != null)
            GetRendererFeatures(_renderer)?.Remove(_feature);

        features.Add(_feature);
        _renderer = renderer;
        Plugin.Log.LogInfo("DLSS renderer feature installed.");
    }

    private void OnBeginCameraRendering(ScriptableRenderContext context, Camera camera)
    {
        var config = Plugin.Instance.ConfigurationHandler;
        if (camera != _camera || !config.DLSSEnabled || !config.DLSSJitterEnabled || config.DLSSJitterStrength <= 0f)
        {
            if (camera == _jitteredCamera)
                RestoreJitteredProjection();
            return;
        }

        _jitterIndex = (_jitterIndex + 1) % HaltonX.Length;
        float strength = Mathf.Clamp01(config.DLSSJitterStrength);
        float jitterX = (HaltonX[_jitterIndex] - 0.5f) * strength;
        float jitterY = (HaltonY[_jitterIndex] - 0.5f) * strength;
        int width = Mathf.Max(camera.pixelWidth, 1);
        int height = Mathf.Max(camera.pixelHeight, 1);

        camera.ResetProjectionMatrix();
        _savedProjectionMatrix = camera.projectionMatrix;
        camera.nonJitteredProjectionMatrix = _savedProjectionMatrix;

        Matrix4x4 jitteredProjection = _savedProjectionMatrix;
        jitteredProjection.m02 += (jitterX / width) * 2f;
        jitteredProjection.m12 += (jitterY / height) * 2f;
        camera.projectionMatrix = jitteredProjection;

        _jitteredCamera = camera;
        _jitterApplied = true;
    }

    private void OnEndCameraRendering(ScriptableRenderContext context, Camera camera)
    {
        if (camera == _jitteredCamera)
            RestoreJitteredProjection();
    }

    private void RestoreJitteredProjection()
    {
        if (!_jitterApplied || _jitteredCamera == null)
            return;

        _jitteredCamera.projectionMatrix = _savedProjectionMatrix;
        _jitteredCamera.nonJitteredProjectionMatrix = _savedProjectionMatrix;
        _jitteredCamera = null;
        _jitterApplied = false;
    }

    private void OnDestroy()
    {
        RestoreJitteredProjection();

        if (_renderer != null && _feature != null)
            GetRendererFeatures(_renderer)?.Remove(_feature);

        if (_feature != null)
        {
            _feature.Dispose();
            Destroy(_feature);
            _feature = null;
        }
    }

    private static List<ScriptableRendererFeature>? GetRendererFeatures(ScriptableRenderer renderer)
    {
        return RendererFeaturesField?.GetValue(renderer) as List<ScriptableRendererFeature>;
    }

    private static float[] GenerateHalton(int radix, int count)
    {
        var values = new float[count];
        for (int i = 0; i < count; i++)
            values[i] = Halton(i + 1, radix);

        return values;
    }

    private static float Halton(int index, int radix)
    {
        float result = 0f;
        float fraction = 1f / radix;

        while (index > 0)
        {
            result += (index % radix) * fraction;
            index /= radix;
            fraction /= radix;
        }

        return result;
    }
}