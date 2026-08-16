using BepInEx;
using BepInEx.Logging;

namespace UpscalerLib;

[BepInPlugin(Guid, Name, Version)]
public sealed class Plugin : BaseUnityPlugin
{
    public const string Guid = "com.github.glarmer.UpscalerLib";
    public const string Name = "Upscaler Lib";
    public const string Version = "1.0.0";

    internal static ManualLogSource Log { get; private set; } = null!;

    private void Awake()
    {
        Log = Logger;
        Log.LogInfo($"{Name} {Version} loaded.");
    }
}