using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace VanillaUIPlus;

[HarmonyPatch(typeof(Page_SelectStoryteller), nameof(Page_SelectStoryteller.DoWindowContents))]
public static class Patch_Page_SelectStoryteller_DoWindowContents
{
    public static void Postfix(Rect rect, StorytellerDef ___storyteller, DifficultyDef ___difficulty, Difficulty ___difficultyValues)
    {
        if (!NewGameDefaults.Enabled)
        {
            return;
        }

        Rect button = NewGameDefaults.ButtonRect(rect);
        TooltipHandler.TipRegion(button, "VUIP.SetStorytellerDefaultTip".Translate());
        if (!Widgets.ButtonText(button, "VUIP.SetAsDefault".Translate()))
        {
            return;
        }

        if (___storyteller == null || ___difficulty == null)
        {
            Messages.Message("VUIP.ChooseDifficultyFirst".Translate(), MessageTypeDefOf.RejectInput, historical: false);
            return;
        }

        GameInitData? init = Find.GameInitData;
        UiPlusMod.Settings.storytellerDefaults = new StorytellerDefaults
        {
            storyteller = ___storyteller.defName,
            difficulty = ___difficulty.defName,
            difficultySettings = ___difficultyValues != null ? NewGameDefaults.CaptureDifficulty(___difficultyValues) : null,
            anomalyPlaystyle = ___difficultyValues?.AnomalyPlaystyleDef?.defName,
            permadeath = init != null && init.permadeathChosen ? init.permadeath : (bool?)null
        };
        NewGameDefaults.Saved();
    }
}
