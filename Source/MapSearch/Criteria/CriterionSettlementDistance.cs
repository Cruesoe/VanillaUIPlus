using System.Collections.Generic;
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
    public string? factionName;
    public float value = 5f;
    public CompareMode mode = CompareMode.AtLeast;

    // Matching settlement tiles on one layer, rebuilt once per frame rather than for every tile searched.
    private List<PlanetTile>? targets;
    private PlanetLayer? targetsLayer;
    private int targetsFrame = -1;

    public override string Label => "VUIP.WorldQuerySettlementDistance".Translate();

    public override float ControlsHeight => ControlRowHeight * 2f;

    public override void DrawControls(Rect rect, ref bool changed)
    {
        FactionFilter.Draw(rect.TopHalf(), faction, factionId, factionName, (match, id, name) =>
        {
            faction = match;
            factionId = id;
            factionName = name;
            targetsFrame = -1;
            WorldQueryPanel.NotifyChanged();
        });
        CompareControls.Draw(rect.BottomHalf(), ref value, ref mode, 1f, 50f, Step, allowExactly: false,
            v => "VUIP.WorldQueryTiles".Translate(v.ToString("F0")), ref changed);
    }

    public override bool Matches(Tile tile)
    {
        float nearest = float.MaxValue;
        PlanetLayer layer = tile.Layer;
        foreach (PlanetTile target in Targets(layer))
        {
            nearest = Mathf.Min(nearest, layer.ApproxDistanceInTiles(tile.tile, target));
        }

        return CompareControls.Test(nearest, value, mode, Step);
    }

    public override WorldCriterion Copy()
    {
        CriterionSettlementDistance copy = (CriterionSettlementDistance)base.Copy();
        copy.targets = null;
        copy.targetsFrame = -1;
        return copy;
    }

    public override void ExposeData()
    {
        Scribe_Values.Look(ref faction, "faction", FactionMatch.NotPlayer);
        Scribe_Values.Look(ref factionId, "factionId", -1);
        Scribe_Values.Look(ref factionName, "factionName");
        Scribe_Values.Look(ref value, "value", 5f);
        Scribe_Values.Look(ref mode, "mode", CompareMode.AtLeast);
    }

    private List<PlanetTile> Targets(PlanetLayer layer)
    {
        if (targets != null && targetsFrame == Time.frameCount && targetsLayer == layer)
        {
            return targets;
        }

        targets ??= new List<PlanetTile>();
        targets.Clear();
        targetsLayer = layer;
        targetsFrame = Time.frameCount;
        foreach (Settlement settlement in Find.WorldObjects.Settlements)
        {
            if (settlement.Tile.Layer == layer && FactionFilter.Matches(settlement.Faction, faction, factionId, factionName))
            {
                targets.Add(settlement.Tile);
            }
        }

        return targets;
    }
}
