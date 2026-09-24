using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using HarmonyLib;
using RimWorld;
using Verse;

namespace VanillaUIPlus;

public static class TitleScreenOptions
{
    private static FileInfo? latestSave;
    private static bool saveCacheInitialized;

    public static void RefreshLatestSave()
    {
        try
        {
            latestSave = GenFilePaths.AllSavedGameFiles.FirstOrDefault();
        }
        catch (Exception exception)
        {
            latestSave = null;
            Log.Warning($"[Vanilla UI+] Could not find the latest save for the Continue button.\n{exception}");
        }

        saveCacheInitialized = true;
    }

    // Runs for every option listing each frame, so the labels are translated once per language.
    public static void ModifyOptions(List<ListableOption> options)
    {
        if (Current.ProgramState != ProgramState.Entry)
        {
            return;
        }

        EnsureLabels();
        if (IndexOf(options, optionsLabel) < 0 || IndexOf(options, quitLabel) < 0)
        {
            return;
        }

        if (!saveCacheInitialized)
        {
            RefreshLatestSave();
        }

        if (UiPlusMod.Settings.hideTutorialButton)
        {
            int tutorial = IndexOf(options, tutorialLabel);
            if (tutorial >= 0)
            {
                options.RemoveAt(tutorial);
            }
        }

        if (UiPlusMod.Settings.showContinueButton && latestSave != null && IndexOf(options, continueLabel) < 0)
        {
            options.Insert(0, new ListableOption(continueLabel, LoadLatestSave));
        }
    }

    private static LoadedLanguage? labelsLanguage;
    private static string optionsLabel = string.Empty;
    private static string quitLabel = string.Empty;
    private static string tutorialLabel = string.Empty;
    private static string continueLabel = string.Empty;

    private static void EnsureLabels()
    {
        if (labelsLanguage == LanguageDatabase.activeLanguage)
        {
            return;
        }

        labelsLanguage = LanguageDatabase.activeLanguage;
        optionsLabel = "Options".Translate();
        quitLabel = "QuitToOS".Translate();
        tutorialLabel = "Tutorial".CanTranslate() ? "Tutorial".Translate() : "LearnToPlay".Translate();
        continueLabel = "VUIP.Continue".Translate();
    }

    private static int IndexOf(List<ListableOption> options, string label)
    {
        for (int i = 0; i < options.Count; i++)
        {
            if (string.Equals(options[i].label, label, StringComparison.Ordinal))
            {
                return i;
            }
        }

        return -1;
    }

    private static void LoadLatestSave()
    {
        RefreshLatestSave();
        if (latestSave == null)
        {
            Messages.Message("VUIP.NoSavedGamesFound".Translate(), MessageTypeDefOf.NeutralEvent);
            return;
        }

        string saveName = Path.GetFileNameWithoutExtension(latestSave.Name);
        Messages.Message("VUIP.ContinueLoading".Translate(saveName), MessageTypeDefOf.SilentInput);
        GameDataSaveLoader.CheckVersionAndLoadGame(saveName);
    }
}

[HarmonyPatch(typeof(MainMenuDrawer), nameof(MainMenuDrawer.Init))]
internal static class MainMenuDrawer_Init_TitleScreenOptionsPatch
{
    [HarmonyPostfix]
    private static void Postfix()
    {
        TitleScreenOptions.RefreshLatestSave();
    }
}

[HarmonyPatch(typeof(OptionListingUtility), nameof(OptionListingUtility.DrawOptionListing))]
internal static class OptionListingUtility_DrawOptionListing_TitleScreenOptionsPatch
{
    [HarmonyPrefix]
    private static void Prefix(List<ListableOption> optList)
    {
        TitleScreenOptions.ModifyOptions(optList);
    }
}
