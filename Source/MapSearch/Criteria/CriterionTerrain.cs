using RimWorld;
using RimWorld.Planet;
using Verse;

namespace VanillaUIPlus;

public class CriterionTerrain : WorldCriterion
{
    public Hilliness hilliness;

    public CriterionTerrain()
    {
    }

    public CriterionTerrain(Hilliness hilliness)
    {
        this.hilliness = hilliness;
    }

    public override string Label => Titled("Terrain".Translate(), hilliness.GetLabelCap());

    public override bool Matches(Tile tile)
    {
        return tile.HillinessLabel == hilliness;
    }

    public override void ExposeData()
    {
        Scribe_Values.Look(ref hilliness, "hilliness");
    }
}
