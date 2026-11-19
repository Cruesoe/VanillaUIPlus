using Verse;

namespace VanillaUIPlus;

/// <summary>
/// Whether the improved Gear tab is drawn, and which active mod replaces the same tab if any.
/// </summary>
public static class GearTab
{
    private static readonly string[] ConflictingPackageIds =
    {
        PawnReadout.CombatExtendedPackageId,
        "andromeda.niceinventorytab",
        "sandy.rpgstyleinventory.avilmask.revamped",
        "lazyfridaystudio.uirethemed.inspectortabs",
    };

    private static string? conflictingMod;
    private static bool conflictChecked;

    public static bool Enabled => UiPlusMod.Settings.gearTabEnabled && ConflictingMod == null && GearTabAccess.Available;

    /// <summary>Name of an active mod that replaces or redraws the Gear tab, or null; the mod list can't change without a restart.</summary>
    public static string? ConflictingMod
    {
        get
        {
            if (!conflictChecked)
            {
                conflictChecked = true;
                foreach (string packageId in ConflictingPackageIds)
                {
                    ModMetaData? mod = ModLister.GetActiveModWithIdentifier(packageId, ignorePostfix: true);
                    if (mod != null)
                    {
                        conflictingMod = mod.Name;
                        break;
                    }
                }
            }

            return conflictingMod;
        }
    }
}
