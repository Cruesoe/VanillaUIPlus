using System;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using Verse;

namespace VanillaUIPlus;

/// <summary>
/// Combat Extended adds a wind readout under the date. It does so with a postfix on
/// GlobalControlsUtility.DoDate, which still runs even though Vanilla UI+ replaces the
/// surrounding HUD, so the row appears whether we want it or not.
///
/// CE draws it as a bare 300px label with no bar behind it, which overhangs the bar
/// column, and its full wording ("Moderate breeze, heading Northeast") is far wider than
/// a bar allows. This redraws the same reading in the HUD's own bar, in the same place,
/// split into a direction column and a strength column so it lines up with the date and
/// temperature rows around it.
/// </summary>
public static class CombatExtendedWind
{
    // CE's own ordering, so bucketing an angle here lands on the same compass point its
    // long-form text would have named.
    private static readonly string[] Directions = { "N", "NE", "E", "SE", "S", "SW", "W", "NW" };

    private static readonly Type? TrackerType = AccessTools.TypeByName("CombatExtended.WeatherTracker");

    private static readonly PropertyInfo? BeaufortProperty =
        TrackerType == null ? null : AccessTools.Property(TrackerType, "BeaufortScale");

    private static readonly FieldInfo? WindDirectionField =
        TrackerType == null ? null : AccessTools.Field(TrackerType, "_windDirection");

    // Shown in the direction column when there is no wind to have a direction.
    private const string NoDirection = "--";

    private static string? baseTooltip;

    public static bool Available => BeaufortProperty != null && WindDirectionField != null;

    public static void Draw(object tracker, ref float curBaseY)
    {
        int beaufort = (int)BeaufortProperty!.GetValue(tracker);
        string strength = ("CE_Wind_Beaufort" + beaufort).Translate();

        // CE omits the direction when the wind is calm, so there is nothing to name.
        string direction = NoDirection;
        string full = strength;
        if (beaufort > 0)
        {
            int index = CompassIndex((float)WindDirectionField!.GetValue(tracker));
            direction = Directions[index];
            full = strength + ", " + ("CE_Wind_Direction_" + Directions[index]).Translate();
        }

        // The strength column is narrow enough to clip a longer name, so the tooltip
        // carries the full reading in CE's own wording alongside its explanation.
        baseTooltip ??= "CE_Wind_Tooltip".Translate();
        ReadoutDrawer.DrawExternalSplitRow(direction, strength, full + "\n\n" + baseTooltip, ref curBaseY);
    }

    private static int CompassIndex(float angle)
    {
        const float step = 360f / 8f;
        return Mathf.Clamp(Mathf.RoundToInt((angle - step * 0.5f) / step), 0, Directions.Length - 1);
    }
}

[HarmonyPatch]
public static class Patch_CombatExtended_DoWindGUI
{
    public static bool Prepare()
    {
        return CombatExtendedWind.Available && TargetMethod() != null;
    }

    public static MethodBase? TargetMethod()
    {
        return AccessTools.Method("CombatExtended.WeatherTracker:DoWindGUI");
    }

    public static bool Prefix(object __instance, ref float yPos)
    {
        if (!UiPlusMod.Enabled)
        {
            return true;
        }

        CombatExtendedWind.Draw(__instance, ref yPos);
        return false;
    }
}
