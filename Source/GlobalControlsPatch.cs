using System.Reflection;
using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace VanillaUIPlus.Alerts;

[HarmonyPatch(typeof(GlobalControls), nameof(GlobalControls.GlobalControlsOnGUI))]
public static class Patch_GlobalControls_GlobalControlsOnGUI
{
    private static readonly FieldInfo RowVisibilityField = AccessTools.Field(typeof(GlobalControls), "rowVisibility");
    private static readonly MethodInfo DoCountdownTimerMethod = AccessTools.Method(typeof(GlobalControls), "DoCountdownTimer");

    public static bool Prefix(GlobalControls __instance)
    {
        if (!AlertsMod.Enabled)
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
        ReadoutDrawer.DrawPlaySettings(rowVisibility, worldView: false, ref curBaseY);
        ReadoutDrawer.DrawTimespeed(ref curBaseY);
        DubsTpsDisplay.Draw(ref curBaseY);
        ReadoutDrawer.DrawDate(ref curBaseY);
        Map? map = Find.CurrentMap;
        if (map != null && !map.IsPocketMap)
        {
            ReadoutDrawer.DrawTemperatureAndWeather(ref curBaseY);
        }

        if (map != null)
        {
            ReadoutDrawer.DrawGameConditions(map, ref curBaseY);
        }
        if (DebugViewSettings.showMemoryInfo)
        {
            GlobalControlsUtility.DrawMemoryInfo(leftX, 200f, ref curBaseY);
        }

        if (DebugViewSettings.showTpsCounter)
        {
            GlobalControlsUtility.DrawTpsCounter(leftX, 200f, ref curBaseY);
        }

        if (DebugViewSettings.showFpsCounter)
        {
            GlobalControlsUtility.DrawFpsCounter(leftX, 200f, ref curBaseY);
        }

        if (Prefs.ShowRealtimeClock)
        {
            GlobalControlsUtility.DoRealtimeClock(leftX, 200f, ref curBaseY);
        }

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
}
