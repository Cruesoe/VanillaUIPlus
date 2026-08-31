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
/// with the compass point abbreviated to the short form CE already uses internally.
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

    private static string? tooltip;

    public static bool Available => BeaufortProperty != null && WindDirectionField != null;

    public static void Draw(object tracker, ref float curBaseY)
    {
        int beaufort = (int)BeaufortProperty!.GetValue(tracker);
        string label = ("CE_Wind_Beaufort" + beaufort).Translate();

        // CE omits the direction when there is no wind to have one.
        if (beaufort > 0)
        {
            float angle = (float)WindDirectionField!.GetValue(tracker);
            label = label + ", " + Compass(angle);
        }

        tooltip ??= "CE_Wind_Tooltip".Translate();
        ReadoutDrawer.DrawExternalRow(label, tooltip, ref curBaseY);
    }

    private static string Compass(float angle)
    {
        const float step = 360f / 8f;
        int index = Mathf.Clamp(Mathf.RoundToInt((angle - step * 0.5f) / step), 0, Directions.Length - 1);
        return Directions[index];
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
