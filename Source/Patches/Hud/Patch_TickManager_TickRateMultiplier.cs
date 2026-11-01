using HarmonyLib;
using Verse;

namespace VanillaUIPlus;

[HarmonyPatch(typeof(TickManager), nameof(TickManager.TickRateMultiplier), MethodType.Getter)]
public static class Patch_TickManager_TickRateMultiplier
{
    public static bool Prefix(TickManager __instance, ref float __result)
    {
        if (!UiPlusMod.Enabled)
        {
            return true;
        }

        __result = TimeSpeedControls.TickRate(__instance);
        return false;
    }
}
