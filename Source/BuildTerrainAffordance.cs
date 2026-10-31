using RimWorld;
using Verse;

namespace VanillaUIPlus;

public static class BuildTerrainAffordance
{
    private static DesignatorManager? owner;
    private static PlaySettings? playSettings;
    private static bool previousOverlay;

    public static void Selected(DesignatorManager manager)
    {
        if (!UiPlusMod.Settings.autoBuildTerrainAffordance || Find.CurrentMap == null
            || manager.SelectedDesignator is not Designator_Build { PlacingDef: ThingDef { category: ThingCategory.Building, terrainAffordanceNeeded: not null } })
        {
            return;
        }

        Restore();
        owner = manager;
        playSettings = Find.PlaySettings;
        previousOverlay = playSettings.showTerrainAffordanceOverlay;
        playSettings.showTerrainAffordanceOverlay = true;
    }

    public static void Deselected(DesignatorManager manager)
    {
        if (owner == manager)
        {
            Restore();
        }
    }

    public static void Restore()
    {
        if (playSettings != null)
        {
            playSettings.showTerrainAffordanceOverlay = previousOverlay;
        }

        owner = null;
        playSettings = null;
    }
}
