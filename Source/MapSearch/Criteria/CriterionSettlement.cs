using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace VanillaUIPlus;

public class CriterionSettlement : WorldCriterion
{
    public FactionMatch faction;
    public int factionId = -1;
    public string? factionName;

    public override string Label => "VUIP.WorldQueryHasSettlement".Translate();

    public override float ControlsHeight => ControlRowHeight;

    public override void DrawControls(Rect rect, ref bool changed)
    {
        FactionFilter.Draw(rect, faction, factionId, factionName, (match, id, name) =>
        {
            faction = match;
            factionId = id;
            factionName = name;
            WorldQueryPanel.NotifyChanged();
        });
    }

    public override bool Matches(Tile tile)
    {
        return FactionFilter.Matches(Find.WorldObjects.SettlementAt(tile.tile)?.Faction, faction, factionId, factionName);
    }

    public override void ExposeData()
    {
        Scribe_Values.Look(ref faction, "faction");
        Scribe_Values.Look(ref factionId, "factionId", -1);
        Scribe_Values.Look(ref factionName, "factionName");
    }
}
