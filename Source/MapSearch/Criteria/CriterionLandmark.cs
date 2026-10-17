using System.Collections.Generic;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace VanillaUIPlus;

public class CriterionLandmark : CriterionDefSet<LandmarkDef>
{
    public CriterionLandmark()
    {
    }

    public CriterionLandmark(IEnumerable<LandmarkDef> defs) : base(defs)
    {
    }

    protected override string Title => "VUIP.WorldQueryLandmark".Translate();

    public override bool Matches(Tile tile)
    {
        return tile.Landmark is { } landmark && Defs.Contains(landmark.def);
    }
}
