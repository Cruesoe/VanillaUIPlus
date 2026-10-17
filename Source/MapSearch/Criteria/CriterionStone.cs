using System.Collections.Generic;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace VanillaUIPlus;

public class CriterionStone : CriterionDefSet<ThingDef>
{
    public CriterionStone()
    {
    }

    public CriterionStone(IEnumerable<ThingDef> defs) : base(defs)
    {
    }

    protected override string Title => "VUIP.WorldQueryStone".Translate();

    public override bool Matches(Tile tile)
    {
        foreach (ThingDef rock in Find.World.NaturalRockTypesIn(tile.tile))
        {
            if (Defs.Contains(rock))
            {
                return true;
            }
        }

        return false;
    }
}
