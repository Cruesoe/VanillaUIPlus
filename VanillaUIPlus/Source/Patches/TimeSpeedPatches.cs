using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace VanillaUIPlus;

[HarmonyPatch(typeof(TimeControls), nameof(TimeControls.DoTimeControlsGUI))]
public static class Patch_TimeControls_DoTimeControlsGUI
{
    public static bool Prefix(Rect timerRect)
    {
        if (!UiPlusMod.Enabled)
        {
            return true;
        }

        if (TimeSpeedControls.DrewThisGui)
        {
            return false;
        }

        if (UiPlusMod.Settings.hideSpeedButtons)
        {
            TimeSpeedControls.HandleKeys(Find.TickManager);
            return false;
        }

        TimeSpeedControls.DrawButtons(timerRect);
        return false;
    }
}

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
