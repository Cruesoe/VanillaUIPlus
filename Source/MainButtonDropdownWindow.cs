using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace VanillaUIPlus;

public sealed class MainButtonDropdownWindow : Window
{
    private readonly List<MainButtonLayoutEntry> buttons;
    private readonly Rect moreRect;

    public MainButtonDropdownWindow(List<MainButtonLayoutEntry> buttons, Rect moreRect)
    {
        this.buttons = buttons;
        this.moreRect = moreRect;
        layer = WindowLayer.Super;
        doCloseButton = false;
        doCloseX = false;
        closeOnClickedOutside = true;
        preventCameraMotion = false;
        drawShadow = false;
        soundAppear = null;
        soundClose = null;
    }

    private const float RowHeight = 36f;

    protected override float Margin => 0f;

    private float ColumnWidth => Mathf.Max(moreRect.width, 220f);

    // Rows that fit between the top of the screen and the More button; extra buttons wrap into further columns
    private int RowsPerColumn => Mathf.Clamp(Mathf.FloorToInt(moreRect.y / RowHeight), 1, Mathf.Max(buttons.Count, 1));

    private int Columns => Mathf.Max(1, Mathf.CeilToInt(buttons.Count / (float)RowsPerColumn));

    public override Vector2 InitialSize => new Vector2(ColumnWidth * Columns, RowsPerColumn * RowHeight);

    protected override void SetInitialSizeAndPosition()
    {
        Vector2 size = InitialSize;
        float x = moreRect.xMax - size.x;
        if (x < 0f)
        {
            x = 0f;
        }

        float y = moreRect.y - size.y;
        if (y < 0f)
        {
            y = 0f;
        }

        windowRect = new Rect(x, y, size.x, size.y);
    }

    public override void PreClose()
    {
        base.PreClose();
        MainButtonLayout.DropdownClosedOnFrame = Time.frameCount;
    }

    public override void DoWindowContents(Rect inRect)
    {
        int rows = RowsPerColumn;
        float width = ColumnWidth;
        int slot = 0;
        for (int i = 0; i < buttons.Count; i++)
        {
            MainButtonLayoutEntry entry = buttons[i];
            MainButtonDef? def = entry.CachedDef ?? DefDatabase<MainButtonDef>.GetNamedSilentFail(entry.defName);
            if (def == null)
            {
                continue;
            }

            // Fill each column top to bottom, then move one column right
            Rect row = new Rect(inRect.x + slot / rows * width, inRect.y + slot % rows * RowHeight, width, RowHeight);
            MainButtonPainter.DrawTab(def, entry, row);
            slot++;
            if (Find.MainTabsRoot.OpenTab == def)
            {
                Close(doCloseSound: false);
                return;
            }
        }
    }
}
