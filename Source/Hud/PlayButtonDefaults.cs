using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace VanillaUIPlus;

/// <summary>
/// Default on/off states for the play-settings toggles (vanilla's and other mods'), set the first time each toggle is drawn in a new game.
/// </summary>
public static class PlayButtonDefaults
{
    private const int Cols = 6;
    private const float Size = 24f;
    private const float Gap = 4f;

    // Each toggle's state as last drawn, by button id; lives for the loaded game.
    private static readonly Dictionary<string, bool> CurrentStates = new Dictionary<string, bool>();
    // Toggles already given their default in the current new game.
    private static readonly HashSet<string> Applied = new HashSet<string>();
    private static bool newGame;

    public static void BeginNewGame()
    {
        newGame = true;
        Applied.Clear();
    }

    // A loaded game keeps its own states.
    public static void EndNewGame()
    {
        newGame = false;
        Applied.Clear();
    }

    public static void Forget()
    {
        EndNewGame();
        CurrentStates.Clear();
    }

    // Called for each toggle in the play-settings row before it draws.
    public static void Apply(string id, ref bool state)
    {
        if (newGame && Applied.Add(id) && UiPlusMod.Settings.playButtonDefaults.TryGetValue(id, out bool wanted))
        {
            state = wanted;
        }

        CurrentStates[id] = state;
    }

    public static void DrawSettings(Listing_Standard list)
    {
        SettingsWidgets.Subheader(list, "VUIP.PlayButtonDefaultsSection".Translate());
        list.Label("VUIP.PlayButtonDefaultsTip".Translate());
        list.Gap(6f);
        float gridHeight = Mathf.Max(GridHeight(PlayButtonFilter.MapButtons), GridHeight(PlayButtonFilter.WorldButtons));
        Rect row = list.GetRect(32f + 8f + gridHeight + 28f);
        DrawColumn(row.LeftHalf().ContractedBy(4f), "VUIP.PlayButtonsMap".Translate(), PlayButtonFilter.MapButtons);
        DrawColumn(row.RightHalf().ContractedBy(4f), "VUIP.PlayButtonsWorld".Translate(), PlayButtonFilter.WorldButtons);
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
        Rect currentRect = new Rect(buttonsRect.x, buttonsRect.y, (buttonsRect.width - 4f) / 2f, 24f);
        Rect clearRect = new Rect(currentRect.xMax + 4f, buttonsRect.y, currentRect.width, 24f);
        bool inGame = Current.ProgramState == ProgramState.Playing && AnyCurrent(buttons);
        if (inGame)
        {
            if (Widgets.ButtonText(currentRect, "VUIP.PlayButtonDefaultsUseCurrent".Translate()))
            {
                UseCurrent(buttons);
            }
        }
        else
        {
            GUI.color = new Color(1f, 1f, 1f, 0.4f);
            Widgets.ButtonText(currentRect, "VUIP.PlayButtonDefaultsUseCurrent".Translate(), active: false);
            GUI.color = Color.white;
            TooltipHandler.TipRegion(currentRect, "VUIP.PlayButtonDefaultsNoGame".Translate());
        }

        if (Widgets.ButtonText(clearRect, "VUIP.PlayButtonDefaultsClear".Translate()))
        {
            for (int i = 0; i < buttons.Count; i++)
            {
                UiPlusMod.Settings.playButtonDefaults.Remove(buttons[i].Id);
            }

            SoundDefOf.Tick_Tiny.PlayOneShotOnCamera();
        }

        column.End();
    }

    // Toggles only; a click cycles not set, on, off.
    private static void DrawGrid(Listing_Standard list, List<PlayButtonEntry> buttons)
    {
        int count = ToggleCount(buttons);
        int rows = Mathf.Max(1, Mathf.CeilToInt(count / (float)Cols));
        Rect grid = list.GetRect(rows * Size + (rows - 1) * Gap);
        float gridWidth = Cols * Size + (Cols - 1) * Gap;
        float x0 = grid.x + Mathf.Max(0f, (grid.width - gridWidth) / 2f);
        Dictionary<string, bool> defaults = UiPlusMod.Settings.playButtonDefaults;
        int index = 0;
        for (int i = 0; i < buttons.Count; i++)
        {
            PlayButtonEntry entry = buttons[i];
            if (!entry.IsToggle)
            {
                continue;
            }

            Rect cell = new Rect(x0 + index % Cols * (Size + Gap), grid.y + index / Cols * (Size + Gap), Size, Size);
            index++;
            bool set = defaults.TryGetValue(entry.Id, out bool on);
            GUI.color = set ? Color.white : new Color(1f, 1f, 1f, 0.35f);
            if (entry.Texture != null)
            {
                IconFit.Draw(cell, entry.Texture);
            }

            GUI.color = Color.white;
            if (set)
            {
                GUI.DrawTexture(new Rect(cell.xMax - 11f, cell.yMax - 11f, 12f, 12f), on ? Widgets.CheckboxOnTex : Widgets.CheckboxOffTex);
            }

            Widgets.DrawHighlightIfMouseover(cell);
            if (Mouse.IsOver(cell))
            {
                string state = (set ? on ? "VUIP.PlayButtonDefaultOn" : "VUIP.PlayButtonDefaultOff" : "VUIP.PlayButtonDefaultNone").Translate();
                TooltipHandler.TipRegion(cell, entry.Tooltip.NullOrEmpty() ? state : entry.Tooltip + "\n\n" + state);
            }

            if (Widgets.ButtonInvisible(cell))
            {
                if (!set)
                {
                    defaults[entry.Id] = true;
                }
                else if (on)
                {
                    defaults[entry.Id] = false;
                }
                else
                {
                    defaults.Remove(entry.Id);
                }

                SoundDefOf.Tick_Tiny.PlayOneShotOnCamera();
            }
        }

        list.Gap(4f);
    }

    private static int ToggleCount(List<PlayButtonEntry> buttons)
    {
        int count = 0;
        for (int i = 0; i < buttons.Count; i++)
        {
            if (buttons[i].IsToggle)
            {
                count++;
            }
        }

        return count;
    }

    private static float GridHeight(List<PlayButtonEntry> buttons)
    {
        int rows = Mathf.Max(1, Mathf.CeilToInt(Mathf.Max(ToggleCount(buttons), 1) / (float)Cols));
        return rows * Size + (rows - 1) * Gap;
    }

    private static bool AnyCurrent(List<PlayButtonEntry> buttons)
    {
        for (int i = 0; i < buttons.Count; i++)
        {
            if (buttons[i].IsToggle && CurrentStates.ContainsKey(buttons[i].Id))
            {
                return true;
            }
        }

        return false;
    }

    private static void UseCurrent(List<PlayButtonEntry> buttons)
    {
        for (int i = 0; i < buttons.Count; i++)
        {
            if (buttons[i].IsToggle && CurrentStates.TryGetValue(buttons[i].Id, out bool state))
            {
                UiPlusMod.Settings.playButtonDefaults[buttons[i].Id] = state;
            }
        }

        SoundDefOf.Tick_Tiny.PlayOneShotOnCamera();
    }
}
