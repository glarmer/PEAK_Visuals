using System;
using System.IO;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.NVIDIA;

namespace PEAK_Visuals.Upscaling;

internal static class NvidiaNativeLoader
{
    private const string NVUnityPluginDLL = "NVUnityPlugin.dll";
    private const string NVNGXDLSSDLL = "nvngx_dlss.dll";

    private static bool _attempted;
    private static bool _loaded;
    private static string? _nativeDirectory;
    private static string? _pluginDirectory;

    public static string NativeDirectory => _nativeDirectory ?? GetAssemblyDirectory();

    public static bool EnsureLoaded()
    {
        if (NVUnityPlugin.IsLoaded())
            return true;

        if (_attempted)
            return _loaded || NVUnityPlugin.IsLoaded();

        _attempted = true;
        _pluginDirectory = GetAssemblyDirectory();
        _nativeDirectory = GetExecutableDirectory();

        if (!StageBundledNativeDLLs(_pluginDirectory, _nativeDirectory))
            _nativeDirectory = _pluginDirectory;

        string DLSSPath = Path.Combine(_nativeDirectory, NVNGXDLSSDLL);
        string pluginPath = Path.Combine(_nativeDirectory, NVUnityPluginDLL);

        if (!File.Exists(DLSSPath) || !File.Exists(pluginPath))
        {
            Plugin.Log.LogWarning("DLSS native files are missing. Expected NVUnityPlugin.dll and nvngx_dlss.dll in " + _nativeDirectory);
            return false;
        }

        if (!SetDLLDirectoryW(_nativeDirectory))
            Plugin.Log.LogWarning($"Failed to add DLSS native directory to DLL search path.");

        TryLoadLibrary(DLSSPath);

        try
        {
            _loaded = NVUnityPlugin.Load() || NVUnityPlugin.IsLoaded();

            if (!_loaded)
            {
                Plugin.Log.LogWarning("NVIDIA Unity plugin was found but Unity did not load it. Native directory: " + _nativeDirectory);
            }
        }
        catch (Exception ex)
        {
            Plugin.Log.LogWarning($"NVIDIA Unity plugin load failed: {ex.GetType().Name}: {ex.Message}");
            _loaded = false;
        }

        return _loaded || NVUnityPlugin.IsLoaded();
    }

    private static bool StageBundledNativeDLLs(string sourceDirectory, string destinationDirectory)
    {
        if (string.Equals(sourceDirectory, destinationDirectory, StringComparison.OrdinalIgnoreCase))
            return true;

        bool ok = true;
        ok &= StageBundledNativeDLL(sourceDirectory, destinationDirectory, NVUnityPluginDLL);
        ok &= StageBundledNativeDLL(sourceDirectory, destinationDirectory, NVNGXDLSSDLL);
        return ok;
    }

    private static bool StageBundledNativeDLL(string sourceDirectory, string destinationDirectory, string fileName)
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
            Plugin.Log.LogInfo($"Staged {fileName} to {destinationDirectory} for DLSS.");
            return true;
        }
        catch (Exception ex)
        {
            Plugin.Log.LogWarning($"Failed to stage {fileName} to {destinationDirectory}: {ex.GetType().Name}: {ex.Message}");
            return false;
        }
    }

    private static string GetAssemblyDirectory()
    {
        string? assemblyLocation = typeof(Plugin).Assembly.Location;
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
        catch
        {
        }

        return Directory.GetCurrentDirectory();
    }

    private static bool TryLoadLibrary(string path)
    {
        try
        {
            IntPtr handle = LoadLibraryW(path);
            if (handle != IntPtr.Zero)
                return true;

            Plugin.Log.LogWarning($"Failed to preload {Path.GetFileName(path)}.");
        }
        catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException)
        {
            Plugin.Log.LogWarning($"Windows native library loader is unavailable {ex.GetType().Name}");
        }
        catch (Exception ex)
        {
            Plugin.Log.LogWarning($"Failed to preload {Path.GetFileName(path)}: {ex.GetType().Name}: {ex.Message}");
        }

        return false;
    }

    [DllImport("kernel32", EntryPoint = "LoadLibraryW", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr LoadLibraryW(string fileName);

    [DllImport("kernel32", EntryPoint = "SetDllDirectoryW", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool SetDLLDirectoryW(string pathName);
}