using System;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace VanillaUIPlus;

[HarmonyPatch(typeof(GlobalControls), nameof(GlobalControls.GlobalControlsOnGUI))]
public static class Patch_GlobalControls_GlobalControlsOnGUI
{
    private static readonly FieldInfo? RowVisibilityField = AccessTools.Field(typeof(GlobalControls), "rowVisibility");
    private static readonly MethodInfo? DoCountdownTimerMethod = AccessTools.Method(typeof(GlobalControls), "DoCountdownTimer");

    private static readonly AccessTools.FieldRef<GlobalControls, WidgetRow>? RowVisibility =
        ReflectionGuard.FieldRef<GlobalControls, WidgetRow>(nameof(GlobalControls), "rowVisibility", RowVisibilityField);
    private static readonly Action<Rect, TimedDetectionRaids>? DoCountdownTimer =
        ReflectionGuard.Delegate<Action<Rect, TimedDetectionRaids>>(nameof(GlobalControls), "DoCountdownTimer", DoCountdownTimerMethod);

    private static readonly bool Ready = RowVisibility != null && DoCountdownTimer != null;

    public static bool Prefix(GlobalControls __instance)
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
        float curBaseY = UI.screenHeight;
        curBaseY -= 35f;
        GenUI.DrawTextWinterShadow(new Rect(UI.screenWidth - 270, UI.screenHeight - 450, 270f, 450f));
        curBaseY -= 4f;
        WidgetRow rowVisibility = RowVisibility!(__instance);
        // Drawn without vanilla's gaps, as one stack.
        GlobalControlsUtility.DoPlaySettings(rowVisibility, worldView: false, ref curBaseY);
        GlobalControlsUtility.DoTimespeedControls(leftX, 200f, ref curBaseY);
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
            Text.Anchor = TextAnchor.MiddleRight;
            DoCountdownTimer!(timerRect, timedDetectionRaids);
            Text.Anchor = TextAnchor.UpperLeft;
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

        // The real-time clock is drawn as one of the bars.
        if (Prefs.ShowRealtimeClock)
        {
            ReadoutDrawer.DrawRealtimeClock(ref curBaseY);
        }
    }
}
