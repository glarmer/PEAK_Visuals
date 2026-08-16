using System;
using System.IO;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.NVIDIA;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;
using NvidiaGraphicsDevice = UnityEngine.NVIDIA.GraphicsDevice;

namespace PEAK_Visuals.Upscaling;

internal sealed class PeakDLSSRendererFeature : ScriptableRendererFeature
{
    private PeakDLSSPass _pass = null!;
    private Camera? _targetCamera;

    public override void Create()
    {
        _pass ??= new PeakDLSSPass();
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        if (!Plugin.Instance.ConfigurationHandler.DLSSEnabled)
            return;

        if (_targetCamera == null || renderingData.cameraData.camera != _targetCamera)
            return;

        _pass ??= new PeakDLSSPass();
        renderer.EnqueuePass(_pass);
    }

    public void SetTargetCamera(Camera? camera)
    {
        _targetCamera = camera;
    }

    public void ResetHistory()
    {
        _pass?.ResetHistory();
    }

    public void SetDLSSActive(bool active)
    {
        if (!active)
            _pass?.Dispose();

        SetActive(active);
    }

    protected override void Dispose(bool disposing)
    {
        _pass?.Dispose();
        base.Dispose(disposing);
    }

    private sealed class PeakDLSSPass : ScriptableRenderPass, IDisposable
    {
        private sealed class PassData
        {
            public bool ShouldReinitializeContext;
            public uint InputWidth;
            public uint InputHeight;
            public uint OutputWidth;
            public uint OutputHeight;
            public bool InputIsHDR;
            public bool InvertedDepth;
            public DLSSQuality Quality;
            public DLSSPresetMode PresetMode;
            public DLSSCommandExecutionData ExecutionData;
            public TextureHandle ColorInput;
            public TextureHandle Depth;
            public TextureHandle MotionVectors;
            public TextureHandle ColorOutput;
        }

        private NvidiaGraphicsDevice? _device;
        private DLSSContext? _context;
        private DLSSQuality _contextQuality = DLSSQuality.MaximumQuality;
        private DLSSPresetMode _contextPresetMode = DLSSPresetMode.PresetK;
        private DLSSPreset _contextActivePreset = DLSSPreset.Preset_K;
        private Vector2Int _contextInputResolution;
        private Vector2Int _contextOutputResolution;
        private bool _contextInputWasHDR;
        private int _contextCreateCount;
        private int _contextDestroyCount;
        private bool _resetHistory = true;
        private bool _availabilityChecked;
        private bool _available;

        public PeakDLSSPass()
        {
            renderPassEvent = RenderPassEvent.AfterRenderingPostProcessing;
            profilingSampler = new ProfilingSampler("PEAK Visuals DLSS");
            ConfigureInput(ScriptableRenderPassInput.Depth | ScriptableRenderPassInput.Motion);
        }

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            var config = Plugin.Instance.ConfigurationHandler;
            if (!config.DLSSEnabled || !EnsureAvailable())
                return;

            var resourceData = frameData.Get<UniversalResourceData>();
            if (resourceData.isActiveTargetBackBuffer)
                return;

            var cameraData = frameData.Get<UniversalCameraData>();
            var source = resourceData.cameraColor;
            var depth = resourceData.cameraDepthTexture.IsValid()
                ? resourceData.cameraDepthTexture
                : resourceData.cameraDepth;
            var motionVectors = resourceData.motionVectorColor;

            if (!source.IsValid() || !depth.IsValid() || !motionVectors.IsValid())
                return;

            TextureDesc inputDesc = source.GetDescriptor(renderGraph);
            int inputWidth = Math.Max(inputDesc.width, 1);
            int inputHeight = Math.Max(inputDesc.height, 1);
            int outputWidth = Math.Max(cameraData.camera.pixelWidth, 1);
            int outputHeight = Math.Max(cameraData.camera.pixelHeight, 1);

            TextureDesc outputDesc = inputDesc;
            outputDesc.width = outputWidth;
            outputDesc.height = outputHeight;
            outputDesc.format = GraphicsFormatUtility.GetLinearFormat(inputDesc.format);
            outputDesc.msaaSamples = MSAASamples.None;
            outputDesc.useMipMap = false;
            outputDesc.autoGenerateMips = false;
            outputDesc.useDynamicScale = false;
            outputDesc.anisoLevel = 0;
            outputDesc.discardBuffer = false;
            outputDesc.enableRandomWrite = true;
            outputDesc.name = "_PEAKVisualsDLSSOutput";
            outputDesc.clearBuffer = false;
            outputDesc.filterMode = FilterMode.Bilinear;

            TextureHandle output = renderGraph.CreateTexture(outputDesc);
            DLSSQuality quality = ToDLSSQuality(config.DLSSMode);
            DLSSPresetMode presetMode = config.DLSSPresetMode;
            var inputResolution = new Vector2Int(inputWidth, inputHeight);
            var outputResolution = new Vector2Int(outputWidth, outputHeight);
            bool inputIsHDR = GraphicsFormatUtility.IsHDRFormat(inputDesc.format);

            using (var builder = renderGraph.AddUnsafePass<PassData>("PEAK Visuals DLSS", out var passData, profilingSampler))
            {
                passData.ShouldReinitializeContext = ShouldReinitializeContext(quality, presetMode, inputResolution, outputResolution, inputIsHDR);
                passData.InputWidth = (uint)inputWidth;
                passData.InputHeight = (uint)inputHeight;
                passData.OutputWidth = (uint)outputWidth;
                passData.OutputHeight = (uint)outputHeight;
                passData.InputIsHDR = inputIsHDR;
                passData.InvertedDepth = SystemInfo.usesReversedZBuffer;
                passData.Quality = quality;
                passData.PresetMode = presetMode;
                passData.ColorInput = source;
                passData.Depth = depth;
                passData.MotionVectors = motionVectors;
                passData.ColorOutput = output;
                passData.ExecutionData = CreateExecutionData(inputWidth, inputHeight);

                builder.UseTexture(source);
                builder.UseTexture(depth);
                builder.UseTexture(motionVectors);
                builder.UseTexture(output, AccessFlags.Write);
                builder.AllowGlobalStateModification(true);

                builder.SetRenderFunc((PassData data, UnsafeGraphContext context) =>
                {
                    var cmd = CommandBufferHelpers.GetNativeCommandBuffer(context.cmd);
                    if (data.ShouldReinitializeContext)
                    {
                        DestroyContext(cmd);
                        CreateContext(cmd, data);
                    }

                    if (_context == null || _device == null)
                        return;

                    _context.executeData = data.ExecutionData;
                    var textures = new DLSSTextureTable
                    {
                        colorInput = data.ColorInput,
                        depth = data.Depth,
                        motionVectors = data.MotionVectors,
                        colorOutput = data.ColorOutput
                    };

                    _device.ExecuteDLSS(cmd, _context, textures);
                    _resetHistory = false;
                });
            }

            resourceData.cameraColor = output;
        }

        public void ResetHistory()
        {
            _resetHistory = true;
        }

        public void Dispose()
        {
            if (_context == null || _device == null)
                return;

            var cmd = new CommandBuffer { name = "PEAK Visuals DLSS Dispose" };
            _device.DestroyFeature(cmd, _context);
            Graphics.ExecuteCommandBuffer(cmd);
            cmd.Release();
            _context = null;
            _resetHistory = true;
        }

        private bool EnsureAvailable()
        {
            if (_availabilityChecked)
                return _available;

            _availabilityChecked = true;

            try
            {
                if (!UpscalerLibIntegration.EnsureNvidiaLoaded())
                {
                    Plugin.Log.LogWarning("DLSS unavailable because the nvidia Unity plugin did not load.");
                    return false;
                }

                LogDLSS();

                if (!SystemInfo.graphicsDeviceVendor.ToLowerInvariant().Contains("nvidia"))
                {
                    Plugin.Log.LogWarning("DLSS unavailable as the current GPU is not a Nvidia one.");
                    return false;
                }

                if (SystemInfo.graphicsDeviceType != GraphicsDeviceType.Direct3D11
                    && SystemInfo.graphicsDeviceType != GraphicsDeviceType.Direct3D12)
                {
                    Plugin.Log.LogWarning(
                        "DLSS unavailable because Unity's Nvidia plugin needs a Direct3D graphics device. Current API is " +
                        SystemInfo.graphicsDeviceType + ".");
                    return false;
                }

                string projectID = typeof(Plugin).Assembly.GetName().Name ?? "PEAK_Visuals";
                _device = CreateGraphicsDevice(projectID);
                if (_device == null)
                {
                    Plugin.Log.LogWarning("DLSS unavailable because failed to create nvidia graphics device.");
                    return false;
                }

                _available = _device.IsFeatureAvailable(GraphicsDeviceFeature.DLSS);
                if (!_available)
                    Plugin.Log.LogWarning("DLSS unavailable because nvidia device does not report DLSS support.");
            }
            catch (Exception ex)
            {
                Plugin.Log.LogWarning($"DLSS unavailable: {ex.GetType().Name}: {ex.Message}");
                _available = false;
            }

            return _available;
        }

        private static NvidiaGraphicsDevice? CreateGraphicsDevice(string projectID)
        {
            string nativeDirectory = UpscalerLibIntegration.NativeDirectory;
            string nativeDirectoryWithSeparator = nativeDirectory.EndsWith(Path.DirectorySeparatorChar.ToString(), StringComparison.Ordinal)
                || nativeDirectory.EndsWith(Path.AltDirectorySeparatorChar.ToString(), StringComparison.Ordinal)
                    ? nativeDirectory
                    : nativeDirectory + Path.DirectorySeparatorChar;

            NvidiaGraphicsDevice? device = NvidiaGraphicsDevice.CreateGraphicsDevice(projectID, nativeDirectoryWithSeparator);
            if (device != null)
                return device;

            Plugin.Log.LogWarning("DLSS probe CreateGraphicsDevice 1 returned null.");

            device = NvidiaGraphicsDevice.CreateGraphicsDevice(projectID);
            if (device != null)
                return device;

            Plugin.Log.LogWarning("DLSS probe CreateGraphicsDevice 2 returned null.");

            device = NvidiaGraphicsDevice.CreateGraphicsDevice();
            if (device == null)
                Plugin.Log.LogWarning("DLSS probe CreateGraphicsDevice() 3 returned null.");

            return device;
        }

        private static void LogDLSS()
        {
            Plugin.Log.LogInfo(
                "DLSS probe: " +
                $"Unity={Application.unityVersion}, " +
                $"Platform={Application.platform}, " +
                $"OS='{SystemInfo.operatingSystem}', " +
                $"API={SystemInfo.graphicsDeviceType}, " +
                $"GPU='{SystemInfo.graphicsDeviceName}', " +
                $"Vendor='{SystemInfo.graphicsDeviceVendor}', " +
                $"DeviceVersion='{SystemInfo.graphicsDeviceVersion}', " +
                $"NVPluginLoaded={NVUnityPlugin.IsLoaded()}, " +
                $"NVPluginVersion={NvidiaGraphicsDevice.version}, " +
                $"NativeDir='{UpscalerLibIntegration.NativeDirectory}'");
        }

        private bool ShouldReinitializeContext(
            DLSSQuality quality,
            DLSSPresetMode presetMode,
            Vector2Int inputResolution,
            Vector2Int outputResolution,
            bool inputIsHDR)
        {
            return _context == null
                || _contextQuality != quality
                || _contextPresetMode != presetMode
                || _contextInputResolution != inputResolution
                || _contextOutputResolution != outputResolution
                || _contextInputWasHDR != inputIsHDR;
        }

        private void CreateContext(CommandBuffer cmd, PassData data)
        {
            if (_device == null)
                return;

            DLSSPreset preset = ResolvePreset(data.PresetMode);

            var init = new DLSSCommandInitializationData
            {
                inputRTWidth = data.InputWidth,
                inputRTHeight = data.InputHeight,
                outputRTWidth = data.OutputWidth,
                outputRTHeight = data.OutputHeight,
                quality = data.Quality,
                presetQualityMode = preset,
                presetBalancedMode = preset,
                presetPerformanceMode = preset,
                presetUltraPerformanceMode = preset,
                presetDlaaMode = preset
            };

            init.SetFlag(DLSSFeatureFlags.IsHDR, data.InputIsHDR);
            init.SetFlag(DLSSFeatureFlags.MVLowRes, true);
            init.SetFlag(DLSSFeatureFlags.DepthInverted, data.InvertedDepth);
            init.SetFlag(DLSSFeatureFlags.MVJittered, false);

            var context = _device.CreateFeature(cmd, init);

            if (context == null)
            {
                Plugin.Log.LogWarning("DLSS context creation returned null.");
                return;
            }

            _context = context;
            _contextQuality = data.Quality;
            _contextPresetMode = data.PresetMode;
            _contextActivePreset = preset;
            _contextInputResolution = new Vector2Int((int)data.InputWidth, (int)data.InputHeight);
            _contextOutputResolution = new Vector2Int((int)data.OutputWidth, (int)data.OutputHeight);
            _contextInputWasHDR = data.InputIsHDR;
            _resetHistory = true;
            _contextCreateCount++;
            if (_contextCreateCount <= 8 || _contextCreateCount % 16 == 0)
            {
                Plugin.Log.LogInfo(
                    $"DLSS context created number: {_contextCreateCount}: " +
                    $"{_contextInputResolution.x}x{_contextInputResolution.y} -> " +
                    $"{_contextOutputResolution.x}x{_contextOutputResolution.y}, " +
                    $"quality={_contextQuality}, preset={_contextPresetMode}/{_contextActivePreset}, " +
                    $"hdr={_contextInputWasHDR}");
            }
        }

        private static DLSSPreset ResolvePreset(DLSSPresetMode presetMode)
        {
            return presetMode switch
            {
                DLSSPresetMode.PresetF => DLSSPreset.Preset_F,
                DLSSPresetMode.PresetJ => DLSSPreset.Preset_J,
                DLSSPresetMode.PresetK => DLSSPreset.Preset_K,
                DLSSPresetMode.PresetL => DLSSPreset.Preset_L,
                DLSSPresetMode.PresetM => DLSSPreset.Preset_M,
                _ => DLSSPreset.Preset_K
            };
        }

        private void DestroyContext(CommandBuffer cmd)
        {
            if (_context == null || _device == null)
                return;

            _device.DestroyFeature(cmd, _context);
            _context = null;
            _resetHistory = true;
            _contextDestroyCount++;
            if (_contextDestroyCount <= 8 || _contextDestroyCount % 16 == 0)
                Plugin.Log.LogInfo($"DLSS context destroyed number: {_contextDestroyCount}.");
        }

        private DLSSCommandExecutionData CreateExecutionData(int inputWidth, int inputHeight)
        {
            return new DLSSCommandExecutionData
            {
                mvScaleX = -inputWidth,
                mvScaleY = -inputHeight,
                subrectOffsetX = 0,
                subrectOffsetY = 0,
                subrectWidth = (uint)inputWidth,
                subrectHeight = (uint)inputHeight,
                jitterOffsetX = 0f,
                jitterOffsetY = 0f,
                preExposure = 1f,
                invertYAxis = SystemInfo.graphicsUVStartsAtTop ? 1u : 0u,
                invertXAxis = 0u,
                reset = _resetHistory ? 1 : 0
            };
        }

        private static DLSSQuality ToDLSSQuality(DLSSMode mode)
        {
            return mode switch
            {
                DLSSMode.Balanced => DLSSQuality.Balanced,
                DLSSMode.Performance => DLSSQuality.MaximumPerformance,
                DLSSMode.UltraPerformance => DLSSQuality.UltraPerformance,
                DLSSMode.DLAA => DLSSQuality.DLAA,
                _ => DLSSQuality.MaximumQuality
            };
        }
    }
}