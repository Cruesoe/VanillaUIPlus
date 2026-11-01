using System.Reflection;
using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace VanillaUIPlus;

[HarmonyPatch(typeof(WorldGlobalControls), nameof(WorldGlobalControls.WorldGlobalControlsOnGUI))]
public static class Patch_WorldGlobalControls_WorldGlobalControlsOnGUI
{
    private static readonly FieldInfo? RowVisibilityField = AccessTools.Field(typeof(WorldGlobalControls), "rowVisibility");
    private static readonly AccessTools.FieldRef<WorldGlobalControls, WidgetRow>? RowVisibility =
        ReflectionGuard.FieldRef<WorldGlobalControls, WidgetRow>(nameof(WorldGlobalControls), "rowVisibility", RowVisibilityField);

    private static readonly bool Ready = RowVisibility != null;

    public static bool Prefix(WorldGlobalControls __instance)
    {
        if (!UiPlusMod.Enabled || !Ready)
        {
            return true;
        }

        if (Event.current.type == EventType.Layout)
        {
            return false;
        }

        float leftX = UI.screenWidth - 200f;
        float curBaseY = UI.screenHeight - 4f;
        if (Current.ProgramState == ProgramState.Playing)
        {
            curBaseY -= 35f;
        }

        WidgetRow rowVisibility = RowVisibility!(__instance);
        GlobalControlsUtility.DoPlaySettings(rowVisibility, worldView: true, ref curBaseY);
        if (Current.ProgramState == ProgramState.Playing)
        {
            // Kept flush with the colony HUD, which drops the same gaps.
            GlobalControlsUtility.DoTimespeedControls(leftX, 200f, ref curBaseY);
            if (Find.CurrentMap != null || Find.WorldSelector.AnyObjectOrTileSelected)
            {
                GlobalControlsUtility.DoDate(leftX, 200f, ref curBaseY);
            }

            ReadoutDrawer.DrawGameConditions(Find.World.gameConditionManager, ref curBaseY);
            Alert_HostilesPresent.DrawPinned(ref curBaseY);
        }

        Patch_GlobalControls_GlobalControlsOnGUI.DrawDebugAndClock(leftX, 200f, ref curBaseY);
        if (!Find.WorldTargeter.IsTargeting)
        {
            Find.WorldRoutePlanner.DoRoutePlannerButton(ref curBaseY);
        }

        if (!Find.PlaySettings.lockNorthUp)
        {
            CompassWidget.CompassOnGUI(ref curBaseY);
        }

        if (Current.ProgramState == ProgramState.Playing)
        {
            curBaseY -= 10f;
            Find.LetterStack.LettersOnGUI(curBaseY);
        }

        return false;
    }
}
