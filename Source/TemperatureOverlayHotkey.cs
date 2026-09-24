using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace VanillaUIPlus;

/// <summary>
/// Adds a configurable shortcut for vanilla's temperature overlay. Heat Map already
/// provides the same shortcut and uses the same default key, so this handler stays out
/// of the way whenever that mod is active.
/// </summary>
public static class TemperatureOverlayHotkey
{
    private const string HeatMapPackageId = "Syrus.HeatMap";

    // The active mod list cannot change without a restart, so this is resolved once.
    private static bool? heatMapActive;

    public static bool HeatMapActive => heatMapActive ??= ModsConfig.IsActive(HeatMapPackageId);

    public static void HandleKeys()
    {
        if (!UiPlusMod.Settings.enableTemperatureOverlayHotkey || HeatMapActive)
        {
            return;
        }

        if (Find.CurrentMap == null || !WorldRendererUtility.DrawingMap || Event.current.type != EventType.KeyDown || !VUIPDefOf.VUIP_ToggleTemperatureOverlay.KeyDownEvent)
        {
            return;
        }

        Event.current.Use();
        Find.PlaySettings.showTemperatureOverlay = !Find.PlaySettings.showTemperatureOverlay;
    }
}
