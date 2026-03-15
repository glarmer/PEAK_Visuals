using HarmonyLib;
using UnityEngine.Rendering.Universal;

namespace PEAK_Visuals.Patches;

public static class MainCameraPatch
{
    [HarmonyPatch(typeof(MainCamera), nameof(MainCamera.Awake))]
    class AwakePatch
    {
        [HarmonyPostfix]
        static void Postfix(MainCamera __instance)
        {
            Plugin.Instance.Settings.SetAllCameraSettings();
        }
    }
}