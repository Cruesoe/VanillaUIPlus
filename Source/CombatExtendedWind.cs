using System;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using Verse;

namespace VanillaUIPlus;

/// <summary>
/// Redraws Combat Extended's wind readout under the date as a HUD split bar: direction on the left, strength on the right.
/// </summary>
public static class CombatExtendedWind
{
    // In CE's order, so an angle maps to the compass point CE's own text would name.
    private static readonly string[] Directions = { "N", "NE", "E", "SE", "S", "SW", "W", "NW" };

    private static readonly Type? TrackerType = AccessTools.TypeByName("CombatExtended.WeatherTracker");

    private static readonly PropertyInfo? BeaufortProperty =
        TrackerType == null ? null : AccessTools.Property(TrackerType, "BeaufortScale");

    private static readonly FieldInfo? WindDirectionField =
        TrackerType == null ? null : AccessTools.Field(TrackerType, "_windDirection");

    // Resolved once, since both are read on every GUI pass.
    private static readonly Func<object, int>? BeaufortGetter = TrackerType == null
        ? null
        : ReflectionGuard.UntypedDelegate<int>("WeatherTracker", "BeaufortScale", BeaufortProperty?.GetGetMethod(nonPublic: true));
    private static readonly AccessTools.FieldRef<object, float>? WindDirection = ResolveWindDirection();

    private static AccessTools.FieldRef<object, float>? ResolveWindDirection()
    {
        if (WindDirectionField == null)
        {
            return null;
        }

        try
        {
            return AccessTools.FieldRefAccess<object, float>(WindDirectionField);
        }
        catch (Exception)
        {
            // Falls back to the FieldInfo below rather than losing the row entirely.
            return null;
        }
    }

    // Shown in the direction column when there is no wind to have a direction.
    private const string NoDirection = "--";

    private static string? baseTooltip;
    private static int cachedBeaufort = int.MinValue;
    private static int cachedIndex = int.MinValue;
    private static string cachedDirection = NoDirection;
    private static string cachedStrength = string.Empty;
    private static string cachedTooltip = string.Empty;

    public static bool Available => BeaufortGetter != null && WindDirectionField != null;

    public static void Draw(object tracker, ref float curBaseY)
    {
        int beaufort = BeaufortGetter!(tracker);

        // CE omits the direction when the wind is calm, so there is nothing to name.
        int index = beaufort > 0 ? CompassIndex(ReadWindDirection(tracker)) : -1;

        // The strings are rebuilt only when the Beaufort step or compass point changes.
        if (beaufort != cachedBeaufort || index != cachedIndex)
        {
            cachedBeaufort = beaufort;
            cachedIndex = index;
            cachedStrength = ("CE_Wind_Beaufort" + beaufort).Translate();
            cachedDirection = index < 0 ? NoDirection : Directions[index];

            // The tooltip carries CE's full wording, since the strength column can clip.
            string full = index < 0
                ? cachedStrength
                : cachedStrength + ", " + ("CE_Wind_Direction_" + Directions[index]).Translate();
            baseTooltip ??= "CE_Wind_Tooltip".Translate();
            cachedTooltip = full + "\n\n" + baseTooltip;
        }

        ReadoutDrawer.DrawExternalSplitRow(cachedDirection, cachedStrength, cachedTooltip, ref curBaseY);
    }

    private static float ReadWindDirection(object tracker)
    {
        if (WindDirection != null)
        {
            return WindDirection(tracker);
        }

        return (float)WindDirectionField!.GetValue(tracker);
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
