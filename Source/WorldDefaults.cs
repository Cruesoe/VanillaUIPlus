using System.Collections.Generic;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace VanillaUIPlus;

/// <summary>World generation choices saved with "Set as default", including map size and starting season; never the seed.</summary>
public class WorldDefaults : IExposable
{
    public float planetCoverage = 0.3f;
    public OverallRainfall rainfall = OverallRainfall.Normal;
    public OverallTemperature temperature = OverallTemperature.Normal;
    public OverallPopulation population = OverallPopulation.Normal;
    public LandmarkDensity landmarkDensity = LandmarkDensity.Normal;
    public float pollution = 0.05f;
    public int mapSize = 250;
    public Season startingSeason = Season.Undefined;

    // Selectable factions only, one entry per copy. Hidden factions are the game's to add.
    public List<string> factions = new List<string>();

    public void ExposeData()
    {
        Scribe_Values.Look(ref planetCoverage, "planetCoverage", 0.3f);
        Scribe_Values.Look(ref rainfall, "rainfall", OverallRainfall.Normal);
        Scribe_Values.Look(ref temperature, "temperature", OverallTemperature.Normal);
        Scribe_Values.Look(ref population, "population", OverallPopulation.Normal);
        Scribe_Values.Look(ref landmarkDensity, "landmarkDensity", LandmarkDensity.Normal);
        Scribe_Values.Look(ref pollution, "pollution", 0.05f);
        Scribe_Values.Look(ref mapSize, "mapSize", 250);
        Scribe_Values.Look(ref startingSeason, "startingSeason", Season.Undefined);
        Scribe_Collections.Look(ref factions, "factions", LookMode.Value);
        if (factions == null)
        {
            factions = new List<string>();
        }
    }
}
