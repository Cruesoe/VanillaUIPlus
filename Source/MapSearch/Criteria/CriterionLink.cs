using RimWorld;
using RimWorld.Planet;
using Verse;

namespace VanillaUIPlus;

public class CriterionLink : WorldCriterion
{
    public bool river;

    public CriterionLink()
    {
    }

    public CriterionLink(bool river)
    {
        this.river = river;
    }

    public override string Label => (river ? "River" : "Road").Translate();

    public override bool Matches(Tile tile)
    {
        if (tile is not SurfaceTile surface)
        {
            return false;
        }

        return river ? !surface.Rivers.NullOrEmpty() : !surface.Roads.NullOrEmpty();
    }

    public override void ExposeData()
    {
        Scribe_Values.Look(ref river, "river");
    }
}
