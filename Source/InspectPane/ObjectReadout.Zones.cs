using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace VanillaUIPlus;

/// <summary>
/// Cards for zones other than growing zones and stockpiles: size, and for a fishing zone its fish population, fish owned and whether it is fishing.
/// </summary>
public static partial class ObjectReadout
{
    private static readonly Texture2D SizeTex = ContentFinder<Texture2D>.Get("UI/Icons/MainButtons/ruler-square-compass");
    private static readonly Texture2D FishTex = ContentFinder<Texture2D>.Get("UI/Icons/MainButtons/water");

    private static void AddZoneCards(View current, Zone zone, HashSet<string> covered)
    {
        // Same line as Zone.GetInspectString.
        string sizeLine = $"{"Size".Translate().CapitalizeFirst()}: {zone.CellCount}";
        covered.Add(sizeLine);
        string sizeTitle = "Size".Translate().CapitalizeFirst();
        int cells = zone.CellCount;
        current.Cards.Add(new ReadoutCard
        {
            IconTexture = SizeTex, Caption = sizeTitle, Value = "VUIP.ObjectPaneCells".Translate(cells).Resolve(), Tip = () => Tip(sizeTitle, sizeLine)
        });

        if (zone is Zone_Fishing fishing)
        {
            AddFishingCards(current, fishing);
        }
    }

    private static void AddFishingCards(View current, Zone_Fishing zone)
    {
        PrefixCover populationLines = CoverLabel(current, "FishPopulation".Translate().CapitalizeFirst());
        PrefixCover declineLines = CoverLabel(current, "FishPopulationDecline".Translate().CapitalizeFirst());
        PrefixCover cannotLines = CoverKeyStart(current, "CannotFish") ?? CoverLabel(current, "CannotFish".Translate().CapitalizeFirst());

        float population = zone.Map.waterBodyTracker.FishPopulationAt(zone.Cells[0]);
        string populationTitle = "FishPopulation".Translate().CapitalizeFirst();
        current.Cards.Add(new ReadoutCard
        {
            IconTexture = FishTex, Caption = populationTitle, Value = population.ToString("F0") + " (" + FishingUtility.FishPopulationLabel(population) + ")",
            ValueColor = declineLines.Lines.Count > 0 ? PawnReadoutDrawer.WarningTextColor : Color.white,
            Tip = () => Tip(populationTitle, (populationLines.Joined + declineLines.Joined).TrimEndNewlines())
        });

        string ownedTitle = TextCache.Key("VUIP.ObjectPaneFishOwnedShort");
        int owned = zone.OwnedFishCount;
        current.Cards.Add(new ReadoutCard
        {
            IconTexture = ContentsTex, Caption = ownedTitle, Value = owned.ToString(), Tip = () => Tip(ownedTitle, TextCache.Key("VUIP.ObjectPaneFishOwnedTip"))
        });

        string statusTitle = TextCache.Key("VUIP.ObjectPaneFishingShort");
        bool fishing = zone.ShouldFishNow && !zone.AllFishableCellsFrozen && zone.HasAnyFishableCells;
        current.Cards.Add(new ReadoutCard
        {
            IconTexture = FishTex, Caption = statusTitle, Value = TextCache.Key(fishing ? "VUIP.ObjectPaneFishingNow" : "VUIP.ObjectPaneFishingStopped"),
            ValueColor = fishing ? PawnReadoutDrawer.GoodColor : PawnReadoutDrawer.WarningTextColor,
            Tip = () => Tip(statusTitle, cannotLines.Lines.Count > 0 ? cannotLines.Joined.TrimEndNewlines() : TextCache.Key("VUIP.ObjectPaneFishingNowTip"))
        });
    }
}
