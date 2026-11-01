using HarmonyLib;
using RimWorld;
using UnityEngine;

namespace VanillaUIPlus;

[HarmonyPatch(typeof(InspectPaneUtility), nameof(InspectPaneUtility.PaneSizeFor))]
public static class Patch_InspectPaneUtility_PaneSizeFor
{
    public static void Postfix(IInspectPane pane, ref Vector2 __result)
    {
        if (pane is MainTabWindow_Inspect && (PawnReadout.SelectedPawn() != null || PawnReadout.CardPawn() != null))
        {
            __result.y = PawnReadout.CurrentPaneHeight();
        }
    }
}
