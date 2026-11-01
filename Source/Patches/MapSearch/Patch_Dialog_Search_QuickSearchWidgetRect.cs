using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace VanillaUIPlus;

[HarmonyPatch(typeof(Dialog_Search<Thing>), "QuickSearchWidgetRect")]
public static class Patch_Dialog_Search_QuickSearchWidgetRect
{
    public static void Postfix(Window __instance, ref Rect __result)
    {
        MapSearchUi.AdjustQuickSearchRect(__instance, ref __result);
    }
}
