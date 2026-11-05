using HarmonyLib;
using Verse;

namespace VanillaUIPlus;

// Drops gaps other mods add between play settings icons (Faction Customizer adds one before its button), which would push icons off the grid.
[HarmonyPatch(typeof(WidgetRow), nameof(WidgetRow.Gap))]
public static class Patch_WidgetRow_Gap
{
    internal static bool AddingCellGap;

    public static bool Prefix()
    {
        return !PlayButtonFilter.Filtering || AddingCellGap;
    }
}
