using RimWorld;
using RimWorld.Planet;
using Verse;

namespace VanillaUIPlus;

public class CriterionCanSettle : WorldCriterion
{
    public bool gravship;

    public CriterionCanSettle()
    {
    }

    public CriterionCanSettle(bool gravship)
    {
        this.gravship = gravship;
    }

    public override string Label => (gravship ? "VUIP.WorldQueryCanLandGravship" : "VUIP.WorldQueryCanSettle").Translate();

    public override string? LockedReason => gravship && !ModsConfig.OdysseyActive ? "VUIP.WorldQueryRequiresOdyssey".Translate().ToString() : null;

    public override bool Matches(Tile tile)
    {
        return TileFinder.IsValidTileForNewSettlement(tile.tile, null, gravship);
    }

    public override void ExposeData()
    {
        Scribe_Values.Look(ref gravship, "gravship");
    }
}
