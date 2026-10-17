using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace VanillaUIPlus;

public class CriterionSettlement : WorldCriterion
{
    public FactionMatch faction;
    public int factionId = -1;

    public override string Label => "VUIP.WorldQueryHasSettlement".Translate();

    public override float ControlsHeight => ControlRowHeight;

    public override void DrawControls(Rect rect, ref bool changed)
    {
        FactionFilter.Draw(rect, faction, factionId, (match, id) =>
        {
            faction = match;
            factionId = id;
            WorldQueryPanel.NotifyChanged();
        });
    }

    public override bool Matches(Tile tile)
    {
        return FactionFilter.Matches(Find.WorldObjects.SettlementAt(tile.tile)?.Faction, faction, factionId);
    }

    public override void ExposeData()
    {
        Scribe_Values.Look(ref faction, "faction");
        Scribe_Values.Look(ref factionId, "factionId", -1);
    }
}
