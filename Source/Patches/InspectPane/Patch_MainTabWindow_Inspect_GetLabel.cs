using HarmonyLib;
using RimWorld;
using UnityEngine;

namespace VanillaUIPlus;

// Hands the pane's name to PawnPaneName and leaves the base game nothing to draw, while the pawn readout's header is shown.
[HarmonyPatch(typeof(MainTabWindow_Inspect), nameof(MainTabWindow_Inspect.GetLabel))]
public static class Patch_MainTabWindow_Inspect_GetLabel
{
    public static void Postfix(Rect rect, ref string __result)
    {
        if (PawnPaneName.Capture(__result, rect))
        {
            __result = string.Empty;
        }
    }
}
