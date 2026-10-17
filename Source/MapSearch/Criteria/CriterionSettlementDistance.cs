using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace VanillaUIPlus;

public class CriterionSettlementDistance : WorldCriterion
{
    private const float Step = 1f;

    public FactionMatch faction = FactionMatch.NotPlayer;
    public int factionId = -1;
    public float value = 5f;
    public CompareMode mode = CompareMode.AtLeast;

    public override string Label => "VUIP.WorldQuerySettlementDistance".Translate();

    public override float ControlsHeight => ControlRowHeight * 2f;

    public override void DrawControls(Rect rect, ref bool changed)
    {
        FactionFilter.Draw(rect.TopHalf(), faction, factionId, (match, id) =>
        {
            faction = match;
            factionId = id;
            WorldQueryPanel.NotifyChanged();
        });
        CompareControls.Draw(rect.BottomHalf(), ref value, ref mode, 1f, 50f, Step, allowExactly: false,
            v => "VUIP.WorldQueryTiles".Translate(v.ToString("F0")), ref changed);
    }

    public override bool Matches(Tile tile)
    {
        float nearest = float.MaxValue;
        PlanetLayer layer = tile.Layer;
        foreach (Settlement settlement in Find.WorldObjects.Settlements)
        {
            if (settlement.Tile.Layer == layer && FactionFilter.Matches(settlement.Faction, faction, factionId))
            {
                nearest = Mathf.Min(nearest, layer.ApproxDistanceInTiles(tile.tile, settlement.Tile));
            }
        }

        return CompareControls.Test(nearest, value, mode, Step);
    }

    public override void ExposeData()
    {
        Scribe_Values.Look(ref faction, "faction", FactionMatch.NotPlayer);
        Scribe_Values.Look(ref factionId, "factionId", -1);
        Scribe_Values.Look(ref value, "value", 5f);
        Scribe_Values.Look(ref mode, "mode", CompareMode.AtLeast);
    }
}
