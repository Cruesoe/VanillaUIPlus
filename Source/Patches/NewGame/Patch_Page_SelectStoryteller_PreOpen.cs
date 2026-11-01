using HarmonyLib;
using RimWorld;
using Verse;

namespace VanillaUIPlus;

// Applies only while no difficulty is chosen, so going Back then Next keeps the player's edits.
[HarmonyPatch(typeof(Page_SelectStoryteller), nameof(Page_SelectStoryteller.PreOpen))]
public static class Patch_Page_SelectStoryteller_PreOpen
{
    public static void Postfix(ref StorytellerDef ___storyteller, ref DifficultyDef ___difficulty, ref Difficulty ___difficultyValues)
    {
        StorytellerDefaults? defaults = UiPlusMod.Settings.storytellerDefaults;
        if (!NewGameDefaults.Enabled || defaults == null || ___difficulty != null)
        {
            return;
        }

        StorytellerDef? storyteller = defaults.storyteller != null ? DefDatabase<StorytellerDef>.GetNamedSilentFail(defaults.storyteller) : null;
        if (storyteller != null && storyteller.listVisible)
        {
            ___storyteller = storyteller;
        }

        DifficultyDef? difficulty = defaults.difficulty != null ? DefDatabase<DifficultyDef>.GetNamedSilentFail(defaults.difficulty) : null;
        if (difficulty != null)
        {
            ___difficulty = difficulty;
            ___difficultyValues = new Difficulty(difficulty);
            if (defaults.difficultySettings != null)
            {
                NewGameDefaults.RestoreDifficulty(___difficultyValues, defaults.difficultySettings);
            }

            AnomalyPlaystyleDef? playstyle = defaults.anomalyPlaystyle != null
                ? DefDatabase<AnomalyPlaystyleDef>.GetNamedSilentFail(defaults.anomalyPlaystyle)
                : null;
            if (playstyle != null)
            {
                ___difficultyValues.AnomalyPlaystyleDef = playstyle;
            }

            if (ModsConfig.AnomalyActive && Find.Scenario != null && Find.Scenario.standardAnomalyPlaystyleOnly)
            {
                ___difficultyValues.AnomalyPlaystyleDef = AnomalyPlaystyleDefOf.Standard;
            }
        }

        if (defaults.permadeath.HasValue && Find.GameInitData != null)
        {
            Find.GameInitData.permadeathChosen = true;
            Find.GameInitData.permadeath = defaults.permadeath.Value;
        }
    }
}
