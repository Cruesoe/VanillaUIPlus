using HarmonyLib;
using RimWorld;
using UnityEngine;

namespace VanillaUIPlus;

[HarmonyPatch(typeof(Alert), nameof(Alert.DrawAt))]
public static class Patch_Alert_DrawAt
{
    public static bool Prefix(Alert __instance, float topY, ref Rect __result)
    {
        if (!UiPlusMod.Enabled)
        {
            return true;
        }

        __result = AlertDrawer.DrawAt(__instance, topY);
        return false;
    }
}
