using System.Collections.Generic;
using Verse;

namespace VanillaUIPlus;

/// <summary>Storyteller page choices saved with "Set as default"; defs are stored by defName and skipped if missing.</summary>
public class StorytellerDefaults : IExposable
{
    public string? storyteller;
    public string? difficulty;

    // Custom difficulty as field name -> text, since mod settings load before defs exist.
    public Dictionary<string, string>? difficultySettings;
    public string? anomalyPlaystyle;

    // Null when no reload-anytime/commitment choice was made, so the page still asks.
    public bool? permadeath;

    public void ExposeData()
    {
        Scribe_Values.Look(ref storyteller, "storyteller");
        Scribe_Values.Look(ref difficulty, "difficulty");
        Scribe_Collections.Look(ref difficultySettings, "difficultySettings", LookMode.Value, LookMode.Value);
        Scribe_Values.Look(ref anomalyPlaystyle, "anomalyPlaystyle");
        Scribe_Values.Look(ref permadeath, "permadeath");
    }
}
