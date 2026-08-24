using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace VanillaUIPlus.Alerts;

public sealed class PlayButtonEntry
{
    public string Id = string.Empty;
    public Texture2D? Texture;
    public string Tooltip = string.Empty;
}

public static class PlayButtonFilter
{
    public static readonly List<PlayButtonEntry> MapButtons = new List<PlayButtonEntry>();
    public static readonly List<PlayButtonEntry> WorldButtons = new List<PlayButtonEntry>();

    public static bool Filtering;
    public static int LastDrawnMap;
    public static int LastDrawnWorld;
    public static bool DidDrawMap;
    public static bool DidDrawWorld;

    private static bool worldView;
    private static int drawnThisPass;
    private static bool vanillaSeeded;

    public static void Begin(bool world)
    {
        Filtering = true;
        worldView = world;
        drawnThisPass = 0;
        EnsureVanillaSeeded();
    }

    public static void End()
    {
        if (worldView)
        {
            LastDrawnWorld = drawnThisPass;
            DidDrawWorld = true;
        }
        else
        {
            LastDrawnMap = drawnThisPass;
            DidDrawMap = true;
        }

        Filtering = false;
    }

    public static bool Allow(Texture2D? tex, string? tooltip)
    {
        if (tex == null)
        {
            return true;
        }

        string id = MakeId(tex);
        Register(id, tex, tooltip ?? string.Empty);
        if (!AlertsMod.Settings.IsPlayButtonShown(id))
        {
            return false;
        }

        drawnThisPass++;
        return true;
    }

    public static int LastDrawn(bool world)
    {
        if (world)
        {
            return DidDrawWorld ? LastDrawnWorld : -1;
        }

        return DidDrawMap ? LastDrawnMap : -1;
    }

    public static void NotifyChanged()
    {
        ReadoutDrawer.ResetPlaySettingsHeight();
    }

    public static void DrawSettings(Listing_Standard list, float width)
    {
        EnsureVanillaSeeded();
        Text.Font = GameFont.Small;
        list.Label("VUIA.PlayButtons".Translate());
        list.Label("VUIA.PlayButtonsTip".Translate());
        list.Gap(6f);

        float gridHeight = Mathf.Max(GridHeight(MapButtons), GridHeight(WorldButtons));
        Rect row = list.GetRect(32f + 8f + gridHeight + 28f);
        Rect left = row.LeftHalf().ContractedBy(4f);
        Rect right = row.RightHalf().ContractedBy(4f);
        DrawColumn(left, "VUIA.PlayButtonsMap".Translate(), MapButtons);
        DrawColumn(right, "VUIA.PlayButtonsWorld".Translate(), WorldButtons);
        list.Gap(6f);
    }

    private static void DrawColumn(Rect rect, string title, List<PlayButtonEntry> buttons)
    {
        Listing_Standard column = new Listing_Standard();
        column.Begin(rect);
        Text.Anchor = TextAnchor.MiddleCenter;
        column.Label(title);
        Text.Anchor = TextAnchor.UpperLeft;
        DrawGrid(column, buttons);
        Rect buttonsRect = column.GetRect(24f);
        Rect showRect = new Rect(buttonsRect.x, buttonsRect.y, (buttonsRect.width - 4f) / 2f, 24f);
        Rect hideRect = new Rect(showRect.xMax + 4f, buttonsRect.y, showRect.width, 24f);
        if (Widgets.ButtonText(showRect, "VUIA.PlayButtonsShowAll".Translate()))
        {
            SetAll(buttons, true);
        }

        if (Widgets.ButtonText(hideRect, "VUIA.PlayButtonsHideAll".Translate()))
        {
            SetAll(buttons, false);
        }

        column.End();
    }

    private static void DrawGrid(Listing_Standard list, List<PlayButtonEntry> buttons)
    {
        const int cols = 6;
        const float size = 24f;
        const float gap = 4f;
        int count = buttons.Count;
        int rows = Mathf.Max(1, Mathf.CeilToInt(count / (float)cols));
        Rect grid = list.GetRect(rows * size + (rows - 1) * gap);
        float gridWidth = cols * size + (cols - 1) * gap;
        float x0 = grid.x + Mathf.Max(0f, (grid.width - gridWidth) / 2f);
        for (int i = 0; i < count; i++)
        {
            PlayButtonEntry entry = buttons[i];
            int col = i % cols;
            int row = i / cols;
            Rect cell = new Rect(x0 + col * (size + gap), grid.y + row * (size + gap), size, size);
            bool shown = AlertsMod.Settings.IsPlayButtonShown(entry.Id);
            Color old = GUI.color;
            if (!shown)
            {
                GUI.color = new Color(1f, 1f, 1f, 0.28f);
            }

            if (entry.Texture != null)
            {
                GUI.DrawTexture(cell, entry.Texture);
            }

            GUI.color = old;
            Widgets.DrawHighlightIfMouseover(cell);
            if (!shown)
            {
                Widgets.DrawBox(cell);
            }

            if (!entry.Tooltip.NullOrEmpty())
            {
                TooltipHandler.TipRegion(cell, entry.Tooltip);
            }

            if (Widgets.ButtonInvisible(cell))
            {
                AlertsMod.Settings.SetPlayButtonShown(entry.Id, !shown);
                SoundDefOf.Tick_Tiny.PlayOneShotOnCamera();
            }
        }

        list.Gap(4f);
    }

    private static float GridHeight(List<PlayButtonEntry> buttons)
    {
        const int cols = 6;
        const float size = 24f;
        const float gap = 4f;
        int rows = Mathf.Max(1, Mathf.CeilToInt(Mathf.Max(buttons.Count, 1) / (float)cols));
        return rows * size + (rows - 1) * gap;
    }

    private static void SetAll(List<PlayButtonEntry> buttons, bool shown)
    {
        foreach (PlayButtonEntry entry in buttons)
        {
            AlertsMod.Settings.SetPlayButtonShown(entry.Id, shown);
        }

        SoundDefOf.Tick_Tiny.PlayOneShotOnCamera();
    }

    private static string MakeId(Texture2D tex)
    {
        return (worldView ? "world:" : "map:") + tex.name;
    }

    private static void Register(string id, Texture2D tex, string tooltip)
    {
        List<PlayButtonEntry> list = worldView ? WorldButtons : MapButtons;
        for (int i = 0; i < list.Count; i++)
        {
            if (list[i].Id != id)
            {
                continue;
            }

            list[i].Texture = tex;
            if (!tooltip.NullOrEmpty())
            {
                list[i].Tooltip = tooltip;
            }

            return;
        }

        list.Add(new PlayButtonEntry
        {
            Id = id,
            Texture = tex,
            Tooltip = tooltip
        });
    }

    private static void EnsureVanillaSeeded()
    {
        if (vanillaSeeded)
        {
            return;
        }

        vanillaSeeded = true;
        worldView = false;
        Seed(false, TexButton.ShowLearningHelper, "ShowLearningHelperWhenEmptyToggleButton");
        Seed(false, TexButton.ShowZones, "ZoneVisibilityToggleButton");
        Seed(false, TexButton.ShowBeauty, "ShowBeautyToggleButton");
        Seed(false, TexButton.ShowRoomStats, "ShowRoomStatsToggleButton");
        Seed(false, TexButton.ShowColonistBar, "ShowColonistBarToggleButton");
        Seed(false, TexButton.ShowRoofOverlay, "ShowRoofOverlayToggleButton");
        Seed(false, TexButton.ShowFertilityOverlay, "ShowFertilityOverlayToggleButton");
        Seed(false, TexButton.ShowTerrainAffordanceOverlay, "ShowTerrainAffordanceOverlayToggleButton");
        Seed(false, TexButton.AutoHomeArea, "AutoHomeAreaToggleButton");
        Seed(false, TexButton.AutoRebuild, "AutoRebuildButton");
        Seed(false, TexButton.ShowTemperatureOverlay, "ShowTemperatureOverlayToggleButton");
        Seed(false, TexButton.CategorizedResourceReadout, "CategorizedResourceReadoutToggleButton");
        Seed(false, TexButton.ShowPollutionOverlay, "ShowPollutionOverlayToggleButton");
        Seed(false, TexButton.ShowVacuumOverlay, "ShowVacuumOverlayToggleButton");
        Seed(false, TexButton.SearchButton, "SearchTheMapDesc");
        Seed(false, TexButton.CodexButton, "EntityCodexGizmoTip");
        worldView = true;
        Seed(true, TexButton.ShowColonistBar, "ShowColonistBarToggleButton");
        Seed(true, TexButton.LockNorthUp, "LockNorthUpToggleButton");
        Seed(true, TexButton.ShowImportantLocations, "ShowImportantExpandingIconsToggleButton");
        Seed(true, TexButton.ShowOtherFactionBases, "ShowBasesExpandingIconsToggleButton");
        Seed(true, TexButton.ShowLandmarkIcons, "ShowExpandingLandmarksToggleButton");
        Seed(true, TexButton.UsePlanetDayNightSystem, "UsePlanetDayNightSystemToggleButton");
        Seed(true, TexButton.ShowWorldFeatures, "ShowWorldFeaturesToggleButton");
        Seed(true, TexButton.SearchButton, "SearchTheWorldDesc");
        worldView = false;
    }

    private static void Seed(bool world, Texture2D tex, string tooltipKey)
    {
        worldView = world;
        Register(MakeId(tex), tex, tooltipKey.Translate());
    }
}

[HarmonyPatch(typeof(PlaySettings), nameof(PlaySettings.DoPlaySettingsGlobalControls))]
public static class Patch_PlaySettings_DoPlaySettingsGlobalControls
{
    public static void Prefix(bool worldView)
    {
        if (!AlertsMod.Enabled)
        {
            return;
        }

        PlayButtonFilter.Begin(worldView);
    }

    public static void Postfix()
    {
        if (!PlayButtonFilter.Filtering)
        {
            return;
        }

        PlayButtonFilter.End();
    }
}

[HarmonyPatch(typeof(WidgetRow), nameof(WidgetRow.ToggleableIcon))]
public static class Patch_WidgetRow_ToggleableIcon
{
    public static bool Prefix(Texture2D tex, string tooltip)
    {
        return !PlayButtonFilter.Filtering || PlayButtonFilter.Allow(tex, tooltip);
    }
}

[HarmonyPatch(typeof(WidgetRow), nameof(WidgetRow.ButtonIcon))]
public static class Patch_WidgetRow_ButtonIcon
{
    public static bool Prefix(Texture2D tex, string tooltip, ref bool __result)
    {
        if (!PlayButtonFilter.Filtering || PlayButtonFilter.Allow(tex, tooltip))
        {
            return true;
        }

        __result = false;
        return false;
    }
}
