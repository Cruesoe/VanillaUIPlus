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

    protected override float Margin => 0f;

    public override Vector2 InitialSize
    {
        get
        {
            int count = buttons.Count;
            return new Vector2(Mathf.Max(moreRect.width, 220f), Mathf.Max(count, 1) * 36f);
        }
    }

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
        float y = 0f;
        for (int i = 0; i < buttons.Count; i++)
        {
            MainButtonLayoutEntry entry = buttons[i];
            MainButtonDef? def = entry.CachedDef ?? DefDatabase<MainButtonDef>.GetNamedSilentFail(entry.defName);
            if (def == null)
            {
                continue;
            }

            Rect row = new Rect(inRect.x, inRect.y + y, inRect.width, 36f);
            MainButtonPainter.DrawTab(def, entry, row);
            y += 36f;
            if (Find.MainTabsRoot.OpenTab == def)
            {
                Close(doCloseSound: false);
                return;
            }
        }
    }
}
