using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace VanillaUIPlus.Alerts;

[HarmonyPatch(typeof(Alert), nameof(Alert.DrawAt))]
public static class Patch_Alert_DrawAt
{
    public static bool Prefix(Alert __instance, float topY, ref Rect __result)
    {
        if (!AlertsMod.Enabled)
        {
            return true;
        }

        __result = AlertDrawer.DrawAt(__instance, topY);
        return false;
    }
}

[HarmonyPatch(typeof(Alert), nameof(Alert.Height), MethodType.Getter)]
public static class Patch_Alert_Height
{
    public static bool Prefix(Alert __instance, ref float __result)
    {
        if (!AlertsMod.Enabled)
        {
            return true;
        }

        __result = AlertDrawer.HeightFor(__instance);
        return false;
    }
}

[HarmonyPatch]
public static class Patch_Alert_OnClick
{
    public static IEnumerable<MethodBase> TargetMethods()
    {
        yield return AccessTools.Method(typeof(Alert), "OnClick");
        foreach (System.Type type in typeof(Alert).AllSubclasses())
        {
            MethodInfo? declared = AccessTools.DeclaredMethod(type, "OnClick");
            if (declared != null)
            {
                yield return declared;
            }
        }
    }

    public static bool Prefix(Alert __instance)
    {
        if (!AlertsMod.Enabled)
        {
            return true;
        }

        if (Event.current != null && Event.current.button == 1)
        {
            SnoozeTracker.Snooze(__instance);
            return false;
        }

        return true;
    }
}

[HarmonyPatch(typeof(AlertsReadout), "CheckAddOrRemoveAlert")]
public static class Patch_AlertsReadout_CheckAddOrRemoveAlert
{
    public static void Prefix(Alert alert, ref bool forceRemove)
    {
        if (!AlertsMod.Enabled)
        {
            return;
        }

        if (SnoozeTracker.IsSnoozed(alert))
        {
            forceRemove = true;
        }
    }
}
