using System;
using RimWorld;
using Verse;

namespace VanillaUIPlus;

/// <summary>A play-settings overlay shown while a matching designator is selected, then set back to its previous value.</summary>
public sealed class DesignatorOverlay
{
    public static readonly DesignatorOverlay GrowingZoneFertility = new DesignatorOverlay(
        () => UiPlusMod.Settings.autoGrowingZoneFertility,
        designator => designator is Designator_ZoneAdd_Growing,
        settings => settings.showFertilityOverlay,
        (settings, value) => settings.showFertilityOverlay = value);

    public static readonly DesignatorOverlay BuildTerrainAffordance = new DesignatorOverlay(
        () => UiPlusMod.Settings.autoBuildTerrainAffordance,
        designator => designator is Designator_Build { PlacingDef: ThingDef { category: ThingCategory.Building, terrainAffordanceNeeded: not null } },
        settings => settings.showTerrainAffordanceOverlay,
        (settings, value) => settings.showTerrainAffordanceOverlay = value);

    private readonly Func<bool> enabled;
    private readonly Func<Designator, bool> matches;
    private readonly Func<PlaySettings, bool> getOverlay;
    private readonly Action<PlaySettings, bool> setOverlay;

    private DesignatorManager? owner;
    private PlaySettings? playSettings;
    private bool previousOverlay;

    private DesignatorOverlay(Func<bool> enabled, Func<Designator, bool> matches, Func<PlaySettings, bool> getOverlay, Action<PlaySettings, bool> setOverlay)
    {
        this.enabled = enabled;
        this.matches = matches;
        this.getOverlay = getOverlay;
        this.setOverlay = setOverlay;
    }

    public static void SelectedAll(DesignatorManager manager)
    {
        GrowingZoneFertility.Selected(manager);
        BuildTerrainAffordance.Selected(manager);
    }

    public static void DeselectedAll(DesignatorManager manager)
    {
        GrowingZoneFertility.Deselected(manager);
        BuildTerrainAffordance.Deselected(manager);
    }

    public static void RestoreAll()
    {
        GrowingZoneFertility.Restore();
        BuildTerrainAffordance.Restore();
    }

    public void Restore()
    {
        if (playSettings != null)
        {
            setOverlay(playSettings, previousOverlay);
        }

        owner = null;
        playSettings = null;
    }

    private void Selected(DesignatorManager manager)
    {
        if (!enabled() || Find.CurrentMap == null || !matches(manager.SelectedDesignator))
        {
            return;
        }

        Restore();
        owner = manager;
        playSettings = Find.PlaySettings;
        previousOverlay = getOverlay(playSettings);
        setOverlay(playSettings, true);
    }

    private void Deselected(DesignatorManager manager)
    {
        if (owner == manager)
        {
            Restore();
        }
    }
}
