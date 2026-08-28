using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace VanillaUIPlus;

public sealed class MainButtonIconPicker : Window
{
    private const float Cell = 48f;
    private const float Pad = 4f;
    private readonly MainButtonLayoutEntry entry;
    private Vector2 scroll;

    public MainButtonIconPicker(MainButtonLayoutEntry entry)
    {
        this.entry = entry;
        doCloseX = true;
        absorbInputAroundWindow = true;
        closeOnClickedOutside = true;
    }

    public override Vector2 InitialSize => new Vector2(440f, 520f);

    public override void DoWindowContents(Rect inRect)
    {
        Text.Font = GameFont.Medium;
        Widgets.Label(new Rect(inRect.x, inRect.y, inRect.width, 32f), "VUIP.MainBarIconPicker".Translate());
        Text.Font = GameFont.Small;
        Rect grid = new Rect(inRect.x, inRect.y + 40f, inRect.width, inRect.height - 40f);
        List<string> paths = MainButtonPainter.IconPaths();
        int extra = 2;
        int count = paths.Count + extra;
        int cols = Mathf.Max(1, Mathf.FloorToInt((grid.width - 16f) / (Cell + Pad)));
        int rows = Mathf.CeilToInt(count / (float)cols);
        Rect view = new Rect(0f, 0f, cols * (Cell + Pad), rows * (Cell + Pad));
        Widgets.BeginScrollView(grid, ref scroll, view);
        DrawChoice(CellRect(view, cols, 0), null, "VUIP.MainBarIconDefault".Translate(), IsDefault());
        DrawChoice(CellRect(view, cols, 1), MainButtonPainter.NonePath, "VUIP.MainBarIconNone".Translate(), entry.iconPath == MainButtonPainter.NonePath);
        for (int i = 0; i < paths.Count; i++)
        {
            string path = paths[i];
            DrawChoice(CellRect(view, cols, i + extra), path, null, entry.iconPath == path);
        }

        Widgets.EndScrollView();
    }

    private bool IsDefault()
    {
        return entry.iconPath.NullOrEmpty();
    }

    private static void DrawCaption(Rect rect, string? caption)
    {
        Text.Anchor = TextAnchor.MiddleCenter;
        Text.Font = GameFont.Tiny;
        Widgets.Label(rect.ContractedBy(2f), caption);
        Text.Font = GameFont.Small;
        Text.Anchor = TextAnchor.UpperLeft;
    }

    private static Rect CellRect(Rect view, int cols, int index)
    {
        int col = index % cols;
        int row = index / cols;
        return new Rect(view.x + col * (Cell + Pad), view.y + row * (Cell + Pad), Cell, Cell);
    }

    private void DrawChoice(Rect rect, string? path, string? caption, bool selected)
    {
        Widgets.DrawHighlightIfMouseover(rect);
        if (selected)
        {
            Widgets.DrawBox(rect, 2);
        }

        if (path == null)
        {
            // Preview the icon the def supplies itself, so Default is self-explanatory
            // for a modded button. Defs without an icon still fall back to the caption.
            Texture2D? defIcon = entry.CachedDef?.Icon;
            if (defIcon != null)
            {
                GUI.DrawTexture(rect.ContractedBy(6f), defIcon);
                if (caption != null)
                {
                    TooltipHandler.TipRegion(rect, caption);
                }
            }
            else
            {
                DrawCaption(rect, caption);
            }
        }
        else if (path == MainButtonPainter.NonePath)
        {
            DrawCaption(rect, caption);
        }
        else
        {
            Texture2D? tex = MainButtonPainter.LoadPath(path);
            if (tex != null)
            {
                GUI.DrawTexture(rect.ContractedBy(6f), tex);
            }
        }

        if (Widgets.ButtonInvisible(rect))
        {
            Apply(path);
        }
    }

    private void Apply(string? path)
    {
        SoundDefOf.Tick_High.PlayOneShotOnCamera();
        if (path == null)
        {
            entry.iconPath = string.Empty;
        }
        else
        {
            entry.iconPath = path;
            if (path == MainButtonPainter.NonePath && entry.look == MainButtonLook.IconOnly)
            {
                entry.look = MainButtonLook.TextOnly;
            }
            else if (path != MainButtonPainter.NonePath && entry.look == MainButtonLook.TextOnly)
            {
                entry.look = MainButtonLook.TextAndIcon;
            }
        }

        UiPlusMod.Instance.WriteSettings();
    }
}
