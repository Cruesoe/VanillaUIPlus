using HarmonyLib;
using RimWorld;

namespace VanillaUIPlus;

[HarmonyPatch(typeof(AlertsReadout), "CheckAddOrRemoveAlert")]
public static class Patch_AlertsReadout_CheckAddOrRemoveAlert
{
    public static void Prefix(Alert alert, ref bool forceRemove)
    {
        // The custom HUD draws the hostiles alert pinned above the letters instead.
        if (UiPlusMod.Enabled && alert is Alert_HostilesPresent)
        {
            forceRemove = true;
            return;
        }

        // Snoozes apply with the custom HUD off too.
        if (SnoozeTracker.IsSnoozed(alert) || LockedBuildingAlerts.ShouldHide(alert))
        {
            forceRemove = true;
        }
    }
}
