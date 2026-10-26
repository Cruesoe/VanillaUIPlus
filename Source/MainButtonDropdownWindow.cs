using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace VanillaUIPlus;

public sealed class MainButtonDropdownWindow : Window
{
    private readonly List<MainButtonLayoutEntry> buttons;
    private readonly Rect anchorRect;
    private readonly MainButtonDef? tabAtOpen;
    private bool clickedOutside;

    public MainButtonDropdownWindow(string menuId, List<MainButtonLayoutEntry> buttons, Rect anchorRect)
    {
        MenuId = menuId;
        tabAtOpen = Find.MainTabsRoot.OpenTab;
        this.buttons = buttons;
        this.anchorRect = anchorRect;
        layer = WindowLayer.Super;
        doCloseButton = false;
        doCloseX = false;
        // Closed in Notify_ClickOutsideWindow instead, which leaves the press for the button it landed on
        closeOnClickedOutside = false;
        preventCameraMotion = false;
        drawShadow = false;
        soundAppear = null;
        soundClose = null;
    }

    private const float RowHeight = 36f;

    public string MenuId { get; }

    protected override float Margin => 0f;

    private float ColumnWidth => Mathf.Max(anchorRect.width, 220f);

    // Rows that fit between the top of the screen and the menu button; extra buttons wrap into further columns
    private int RowsPerColumn => Mathf.Clamp(Mathf.FloorToInt(anchorRect.y / RowHeight), 1, Mathf.Max(buttons.Count, 1));

    private int Columns => Mathf.Max(1, Mathf.CeilToInt(buttons.Count / (float)RowsPerColumn));

    public override Vector2 InitialSize => new Vector2(ColumnWidth * Columns, RowsPerColumn * RowHeight);

    protected override void SetInitialSizeAndPosition()
    {
        Vector2 size = InitialSize;
        float x = anchorRect.xMax - size.x;
        if (x < 0f)
        {
            x = 0f;
        }

        float y = anchorRect.y - size.y;
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
        MainButtonLayout.DropdownClosedMenu = MenuId;
    }

    // The stack notifies while iterating its windows, so the close waits for the next draw
    public override void Notify_ClickOutsideWindow()
    {
        base.Notify_ClickOutsideWindow();
        // A press on the menu's own button is left to that button, which closes the menu when the click completes
        if (!anchorRect.Contains(UI.MousePositionOnUIInverted))
        {
            clickedOutside = true;
        }
    }

    public override void DoWindowContents(Rect inRect)
    {
        if (clickedOutside)
        {
            Close(doCloseSound: false);
            return;
        }

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
            // Any entry clicked closes the menu; a worker that draws its own button is caught by the tab it opens
            if (MainButtonPainter.ActivatedOnFrame == Time.frameCount || (Find.MainTabsRoot.OpenTab == def && def != tabAtOpen))
            {
                Close(doCloseSound: false);
                return;
            }
        }
    }
}
