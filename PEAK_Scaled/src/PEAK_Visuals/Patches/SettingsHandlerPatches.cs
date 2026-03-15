using HarmonyLib;

namespace PEAK_Visuals.Patches;

public static class SettingsHandlerPatches
{
    [HarmonyPatch(typeof(SettingsHandler))]
    [HarmonyPatch(MethodType.Constructor)]
    class ConstructorPatch
    {
        [HarmonyPostfix]
        static void Postfix(SettingsHandler __instance)
        {
            Plugin.Instance.Settings.SetAllSettings();
        }
    }

    [HarmonyPatch(typeof(SettingsHandler), nameof(SettingsHandler.SaveSetting))]
    class SaveSettingPatch
    {
        [HarmonyPostfix]
        static void Postfix(SettingsHandler __instance)
        {
            Plugin.Instance.Settings.SetAllSettings();
        }
    }
}