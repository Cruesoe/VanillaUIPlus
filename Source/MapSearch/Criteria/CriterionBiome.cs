using System.Collections.Generic;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace VanillaUIPlus;

public class CriterionBiome : CriterionDefSet<BiomeDef>
{
    public CriterionBiome()
    {
    }

    public CriterionBiome(IEnumerable<BiomeDef> defs) : base(defs)
    {
    }

    protected override string Title => "Biome".Translate();

    public override bool Matches(Tile tile)
    {
        foreach (BiomeDef biome in tile.Biomes)
        {
            if (Defs.Contains(biome))
            {
                return true;
            }
        }

        return false;
    }
}
