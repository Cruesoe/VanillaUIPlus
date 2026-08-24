using System.Reflection;
using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace VanillaUIPlus;

[HarmonyPatch(typeof(GlobalControlsUtility), nameof(GlobalControlsUtility.DoPlaySettings))]
public static class Patch_DoPlaySettings
{
    public static bool Prefix(WidgetRow rowVisibility, bool worldView, ref float curBaseY)
    {
        if (!UiPlusMod.Enabled)
        {
            return true;
        }

        TimeSpeedControls.ResetDrewThisGui();
        ReadoutDrawer.DrawPlaySettings(rowVisibility, worldView, ref curBaseY);
        return false;
    }
}

[HarmonyPatch(typeof(GlobalControlsUtility), nameof(GlobalControlsUtility.DoTimespeedControls))]
public static class Patch_DoTimespeedControls
{
    public static bool Prefix(ref float curBaseY)
    {
        if (!UiPlusMod.Enabled)
        {
            return true;
        }

        TimeSpeedControls.Draw(ref curBaseY);
        DubsTpsDisplay.Draw(ref curBaseY);
        return false;
    }
}

[HarmonyPatch(typeof(GlobalControlsUtility), nameof(GlobalControlsUtility.DoDate))]
public static class Patch_DoDate
{
    public static bool Prefix(ref float curBaseY)
    {
        if (!UiPlusMod.Enabled)
        {
            return true;
        }

        ReadoutDrawer.DrawDate(ref curBaseY);
        return false;
    }
}

[HarmonyPatch(typeof(GlobalControls), nameof(GlobalControls.GlobalControlsOnGUI))]
public static class Patch_GlobalControls_GlobalControlsOnGUI
{
    private static readonly FieldInfo RowVisibilityField = AccessTools.Field(typeof(GlobalControls), "rowVisibility");
    private static readonly MethodInfo DoCountdownTimerMethod = AccessTools.Method(typeof(GlobalControls), "DoCountdownTimer");

    public static bool Prefix(GlobalControls __instance)
    {
        if (!UiPlusMod.Enabled)
        {
            return true;
        }

        if (Event.current.type == EventType.Layout)
        {
            return false;
        }

        float leftX = UI.screenWidth - 200f;
        float curBaseY = UI.screenHeight;
        curBaseY -= 35f;
        GenUI.DrawTextWinterShadow(new Rect(UI.screenWidth - 270, UI.screenHeight - 450, 270f, 450f));
        curBaseY -= 4f;
        WidgetRow rowVisibility = (WidgetRow)RowVisibilityField.GetValue(__instance);
        GlobalControlsUtility.DoPlaySettings(rowVisibility, worldView: false, ref curBaseY);
        curBaseY -= 4f;
        GlobalControlsUtility.DoTimespeedControls(leftX, 200f, ref curBaseY);
        curBaseY -= 4f;
        GlobalControlsUtility.DoDate(leftX, 200f, ref curBaseY);
        Map? map = Find.CurrentMap;
        if (map != null)
        {
            ReadoutDrawer.DrawTemperatureAndWeather(ref curBaseY, showWeather: !map.IsPocketMap);
            ReadoutDrawer.DrawGameConditions(map.gameConditionManager, ref curBaseY);
        }

        Alert_HostilesPresent.DrawPinned(ref curBaseY);
        DrawDebugAndClock(leftX, 200f, ref curBaseY);
        TimedDetectionRaids? timedDetectionRaids = map?.Parent?.GetComponent<TimedDetectionRaids>();
        if (timedDetectionRaids != null && timedDetectionRaids.NextRaidCountdownActiveAndVisible)
        {
            Rect timerRect = new Rect(leftX, curBaseY - 26f, 193f, 26f);
            DoCountdownTimerMethod.Invoke(null, new object[] { timerRect, timedDetectionRaids });
            curBaseY -= 26f;
        }

        curBaseY -= 10f;
        Find.LetterStack.LettersOnGUI(curBaseY);
        return false;
    }

    internal static void DrawDebugAndClock(float leftX, float width, ref float curBaseY)
    {
        if (DebugViewSettings.showMemoryInfo)
        {
            GlobalControlsUtility.DrawMemoryInfo(leftX, width, ref curBaseY);
        }

        if (DebugViewSettings.showTpsCounter)
        {
            GlobalControlsUtility.DrawTpsCounter(leftX, width, ref curBaseY);
        }

        if (DebugViewSettings.showFpsCounter)
        {
            GlobalControlsUtility.DrawFpsCounter(leftX, width, ref curBaseY);
        }

        if (Prefs.ShowRealtimeClock)
        {
            GlobalControlsUtility.DoRealtimeClock(leftX, width, ref curBaseY);
        }
    }
}

[HarmonyPatch(typeof(WorldGlobalControls), nameof(WorldGlobalControls.WorldGlobalControlsOnGUI))]
public static class Patch_WorldGlobalControls_WorldGlobalControlsOnGUI
{
    private static readonly FieldInfo RowVisibilityField = AccessTools.Field(typeof(WorldGlobalControls), "rowVisibility");

    public static bool Prefix(WorldGlobalControls __instance)
    {
        if (!UiPlusMod.Enabled)
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

        WidgetRow rowVisibility = (WidgetRow)RowVisibilityField.GetValue(__instance);
        GlobalControlsUtility.DoPlaySettings(rowVisibility, worldView: true, ref curBaseY);
        if (Current.ProgramState == ProgramState.Playing)
        {
            curBaseY -= 4f;
            GlobalControlsUtility.DoTimespeedControls(leftX, 200f, ref curBaseY);
            if (Find.CurrentMap != null || Find.WorldSelector.AnyObjectOrTileSelected)
            {
                curBaseY -= 4f;
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
