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

    public static void ModifyOptions(List<ListableOption> options)
    {
        if (Current.ProgramState != ProgramState.Entry || !IsTitleScreenList(options))
        {
            return;
        }

        if (!saveCacheInitialized)
        {
            RefreshLatestSave();
        }

        if (UiPlusMod.Settings.hideTutorialButton)
        {
            string tutorialLabel = "Tutorial".CanTranslate()
                ? "Tutorial".Translate().ToString()
                : "LearnToPlay".Translate().ToString();
            options.RemoveAll(option => string.Equals(option.label, tutorialLabel, StringComparison.Ordinal));
        }

        if (!UiPlusMod.Settings.showContinueButton || latestSave == null)
        {
            return;
        }

        string continueLabel = "Continue".Translate().ToString();
        if (options.Any(option => string.Equals(option.label, continueLabel, StringComparison.Ordinal)))
        {
            return;
        }

        options.Insert(0, new ListableOption(continueLabel, LoadLatestSave));
    }

    private static bool IsTitleScreenList(List<ListableOption> options)
    {
        string optionsLabel = "Options".Translate().ToString();
        string quitLabel = "QuitToOS".Translate().ToString();
        return options.Any(option => string.Equals(option.label, optionsLabel, StringComparison.Ordinal))
            && options.Any(option => string.Equals(option.label, quitLabel, StringComparison.Ordinal));
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
    [HarmonyAfter("com.phoenix.continuebuttonmod")]
    private static void Prefix(List<ListableOption> optList)
    {
        TitleScreenOptions.ModifyOptions(optList);
    }
}
