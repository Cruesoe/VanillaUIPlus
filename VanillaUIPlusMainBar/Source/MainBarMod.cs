using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using Verse;

namespace VanillaUIPlus;

public class MainBarMod : Mod
{
    public const string MainBarMigrationFile = "VanillaUIPlus_mainButtons_migration.xml";

    public static MainBarSettings Settings = null!;
    public static MainBarMod Instance = null!;

    public static bool Enabled => Settings.enabled;

    private Vector2 settingsScroll;
    private float settingsHeight;

    public MainBarMod(ModContentPack content) : base(content)
    {
        Instance = this;
        Settings = GetSettings<MainBarSettings>();
        TryMigrateFromUiPlus();
    }

    public override string SettingsCategory()
    {
        return "VUIPMB.SettingsCategory".Translate();
    }

    public override void DoSettingsWindowContents(Rect inRect)
    {
        float viewWidth = inRect.width - 16f;
        Rect view = new Rect(0f, 0f, viewWidth, Mathf.Max(settingsHeight, inRect.height));
        Widgets.BeginScrollView(inRect, ref settingsScroll, view);
        Listing_Standard list = new Listing_Standard
        {
            maxOneColumn = true
        };
        list.Begin(view);

        DrawResetHeader(list, "VUIPMB.ResetTip".Translate(), ResetToVanilla);
        list.CheckboxLabeled("VUIPMB.Enabled".Translate(), ref Settings.enabled, "VUIPMB.EnabledTip".Translate());
        list.GapLine();
        MainButtonLayout.DrawSettings(list);

        list.End();
        settingsHeight = list.CurHeight + 12f;
        Widgets.EndScrollView();
    }

    private void TryMigrateFromUiPlus()
    {
        if (Settings.mainButtons != null && Settings.mainButtons.Count > 0)
        {
            return;
        }

        string config = GenFilePaths.ConfigFolderPath;
        if (!Directory.Exists(config))
        {
            return;
        }

        string[] files;
        try
        {
            List<string> found = new List<string>();
            string snapshot = Path.Combine(config, MainBarMigrationFile);
            if (File.Exists(snapshot))
            {
                found.Add(snapshot);
            }

            found.AddRange(Directory.GetFiles(config, "Mod_*_UiPlusMod.xml"));
            files = found.ToArray();
        }
        catch (Exception)
        {
            return;
        }

        List<MainButtonLayoutEntry>? best = null;
        for (int i = 0; i < files.Length; i++)
        {
            List<MainButtonLayoutEntry>? loaded = ReadMainButtons(files[i]);
            if (loaded == null || loaded.Count == 0)
            {
                continue;
            }

            if (best == null || loaded.Count > best.Count)
            {
                best = loaded;
            }
        }

        if (best == null)
        {
            return;
        }

        Settings.mainButtons = best;
        WriteSettings();
    }

    private static List<MainButtonLayoutEntry>? ReadMainButtons(string path)
    {
        LegacyMainButtons? loaded = new LegacyMainButtons();
        try
        {
            Scribe.loader.InitLoading(path);
            try
            {
                Scribe_Deep.Look(ref loaded, "ModSettings");
            }
            finally
            {
                Scribe.loader.FinalizeLoading();
            }
        }
        catch (Exception e)
        {
            Log.Warning("Vanilla UI+ Main Bar: could not copy main bar settings from " + path + ". " + e);
            return null;
        }

        return loaded?.mainButtons;
    }

    private static void ResetToVanilla()
    {
        Settings.enabled = true;
        MainButtonLayout.ResetToVanilla();
        Instance.WriteSettings();
    }

    private static void DrawResetHeader(Listing_Standard listing, string resetTip, Action onReset)
    {
        Rect row = listing.GetRect(30f);
        Rect resetRect = new Rect(row.xMax - 110f, row.y, 110f, 30f);
        TooltipHandler.TipRegion(resetRect, resetTip);
        if (Widgets.ButtonText(resetRect, "Reset".Translate()))
        {
            onReset();
        }

        listing.GapLine();
    }
}

public class MainBarSettings : ModSettings
{
    public bool enabled = true;
    public List<MainButtonLayoutEntry> mainButtons = new List<MainButtonLayoutEntry>();

    public override void ExposeData()
    {
        Scribe_Values.Look(ref enabled, "enabled", true);
        Scribe_Collections.Look(ref mainButtons, "mainButtons", LookMode.Deep);
        if (mainButtons == null)
        {
            mainButtons = new List<MainButtonLayoutEntry>();
        }
    }
}

public class LegacyMainButtons : IExposable
{
    public List<MainButtonLayoutEntry> mainButtons = new List<MainButtonLayoutEntry>();

    public void ExposeData()
    {
        Scribe_Collections.Look(ref mainButtons, "mainButtons", LookMode.Deep);
        if (mainButtons == null)
        {
            mainButtons = new List<MainButtonLayoutEntry>();
        }
    }
}
