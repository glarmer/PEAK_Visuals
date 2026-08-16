using System;
using System.Reflection;
using BepInEx.Bootstrap;

namespace PEAK_Visuals.Upscaling;

internal static class UpscalerLibIntegration
{
    public const string PluginGuid = "com.github.glarmer.UpscalerLib";

    private const string NvidiaNativeLoaderTypeName = "UpscalerLib.Native.NvidiaNativeLoader";

    private static bool _resolved;
    private static Type? _nvidiaNativeLoaderType;
    private static MethodInfo? _ensureLoadedMethod;
    private static PropertyInfo? _nativeDirectoryProperty;

    public static bool IsInstalled => Chainloader.PluginInfos.ContainsKey(PluginGuid);

    public static string NativeDirectory
    {
        get
        {
            Resolve();
            return _nativeDirectoryProperty?.GetValue(null) as string ?? string.Empty;
        }
    }

    public static bool EnsureNvidiaLoaded()
    {
        if (!IsInstalled)
            return false;

        Resolve();
        if (_ensureLoadedMethod == null)
            return false;

        try
        {
            return _ensureLoadedMethod.Invoke(null, [Plugin.Log]) is true;
        }
        catch (Exception ex)
        {
            Plugin.Log.LogWarning($"UpscalerLib NVIDIA loader failed {ex.GetType().Name}: {ex.Message}");
            return false;
        }
    }

    private static void Resolve()
    {
        if (_resolved)
            return;

        _resolved = true;

        foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            _nvidiaNativeLoaderType = assembly.GetType(NvidiaNativeLoaderTypeName, false);
            if (_nvidiaNativeLoaderType != null)
                break;
        }

        if (_nvidiaNativeLoaderType == null)
            return;

        _ensureLoadedMethod = _nvidiaNativeLoaderType.GetMethod("EnsureLoaded", BindingFlags.Public | BindingFlags.Static,
            null, [typeof(BepInEx.Logging.ManualLogSource)], null);

        _nativeDirectoryProperty = _nvidiaNativeLoaderType.GetProperty("NativeDirectory", BindingFlags.Public | BindingFlags.Static);
    }
}
