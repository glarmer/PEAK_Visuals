using System;
using System.IO;
using System.Runtime.InteropServices;
using BepInEx.Logging;
using UnityEngine;
using UnityEngine.NVIDIA;

namespace UpscalerLib.Native;

public static class NvidiaNativeLoader
{
    private const string NVUnityPluginDLL = "NVUnityPlugin.dll";
    private const string NVNGXDLSSDLL = "nvngx_dlss.dll";

    private static bool _attempted;
    private static bool _loaded;
    private static string? _nativeDirectory;
    private static string? _pluginDirectory;
    private static ManualLogSource? _fallbackLog;

    public static string NativeDirectory => _nativeDirectory ?? GetAssemblyDirectory();

    public static bool EnsureLoaded(ManualLogSource? logger = null)
    {
        logger ??= Plugin.Log ?? (_fallbackLog ??= BepInEx.Logging.Logger.CreateLogSource("Upscaler Lib"));

        if (NVUnityPlugin.IsLoaded())
            return true;

        if (_attempted)
            return _loaded || NVUnityPlugin.IsLoaded();

        _attempted = true;
        _pluginDirectory = GetAssemblyDirectory();
        _nativeDirectory = GetExecutableDirectory();

        if (!StageBundledNativeDLLs(_pluginDirectory, _nativeDirectory, logger))
            _nativeDirectory = _pluginDirectory;

        string dlssPath = Path.Combine(_nativeDirectory, NVNGXDLSSDLL);
        string pluginPath = Path.Combine(_nativeDirectory, NVUnityPluginDLL);

        if (!File.Exists(dlssPath) || !File.Exists(pluginPath))
        {
            logger.LogWarning("DLSS native files are missing. NVUnityPlugin.dll and nvngx_dlss.dll should be in " + _nativeDirectory);
            return false;
        }

        if (!SetDLLDirectoryW(_nativeDirectory))
            logger.LogWarning("Failed to add DLSS native directory to DLL search path.");

        TryLoadLibrary(dlssPath, logger);

        try
        {
            _loaded = NVUnityPlugin.Load() || NVUnityPlugin.IsLoaded();

            if (!_loaded)
            {
                logger.LogWarning("NVIDIA Unity plugin was found but Unity did not load it. Directory " + _nativeDirectory);
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning($"NVIDIA Unity plugin load failed: {ex.GetType().Name}: {ex.Message}");
            _loaded = false;
        }

        return _loaded || NVUnityPlugin.IsLoaded();
    }

    private static bool StageBundledNativeDLLs(string sourceDirectory, string destinationDirectory, ManualLogSource logger)
    {
        if (string.Equals(sourceDirectory, destinationDirectory, StringComparison.OrdinalIgnoreCase))
            return true;

        bool ok = true;
        ok &= StageBundledNativeDLL(sourceDirectory, destinationDirectory, NVUnityPluginDLL, logger);
        ok &= StageBundledNativeDLL(sourceDirectory, destinationDirectory, NVNGXDLSSDLL, logger);
        return ok;
    }

    private static bool StageBundledNativeDLL(string sourceDirectory, string destinationDirectory, string fileName, ManualLogSource logger)
    {
        string source = Path.Combine(sourceDirectory, fileName);
        string destination = Path.Combine(destinationDirectory, fileName);

        if (!File.Exists(source))
            return false;

        try
        {
            if (File.Exists(destination) && new FileInfo(destination).Length == new FileInfo(source).Length)
                return true;

            File.Copy(source, destination, true);
            logger.LogInfo($"Staged {fileName} to {destinationDirectory} for DLSS.");
            return true;
        }
        catch (Exception ex)
        {
            logger.LogWarning($"Failed to stage {fileName} to {destinationDirectory}: {ex.GetType().Name}: {ex.Message}");
            return false;
        }
    }

    private static string GetAssemblyDirectory()
    {
        string? assemblyLocation = typeof(NvidiaNativeLoader).Assembly.Location;
        string? directory = string.IsNullOrEmpty(assemblyLocation)
            ? null
            : Path.GetDirectoryName(assemblyLocation);

        return string.IsNullOrEmpty(directory) ? Directory.GetCurrentDirectory() : directory;
    }

    private static string GetExecutableDirectory()
    {
        try
        {
            string dataPath = Application.dataPath;
            if (!string.IsNullOrEmpty(dataPath))
            {
                string? directory = Path.GetDirectoryName(dataPath);
                if (!string.IsNullOrEmpty(directory))
                    return directory;
            }
        }
        catch (Exception ex)
        {
            Plugin.Log.LogError(ex.StackTrace);
        }

        return Directory.GetCurrentDirectory();
    }

    private static bool TryLoadLibrary(string path, ManualLogSource logger)
    {
        try
        {
            IntPtr handle = LoadLibraryW(path);
            if (handle != IntPtr.Zero)
                return true;

            logger.LogWarning($"Failed to preload {Path.GetFileName(path)}.");
        }
        catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException)
        {
            logger.LogWarning($"Windows native library loader is unavailable {ex.GetType().Name}");
        }
        catch (Exception ex)
        {
            logger.LogWarning($"Failed to preload {Path.GetFileName(path)}: {ex.GetType().Name}: {ex.Message}");
        }

        return false;
    }

    [DllImport("kernel32", EntryPoint = "LoadLibraryW", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr LoadLibraryW(string fileName);

    [DllImport("kernel32", EntryPoint = "SetDllDirectoryW", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool SetDLLDirectoryW(string pathName);
}