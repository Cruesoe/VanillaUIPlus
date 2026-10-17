using System;
using UnityEngine;
using Verse;

namespace VanillaUIPlus;

public sealed class Dialog_NameMainButtonMenu : Window
{
    private const int MaxNameLength = 40;
    private readonly Action<string> onAccept;
    private string name;
    private bool focused;

    public Dialog_NameMainButtonMenu(string name, Action<string> onAccept)
    {
        this.name = name;
        this.onAccept = onAccept;
        forcePause = false;
        closeOnClickedOutside = true;
        absorbInputAroundWindow = true;
        doCloseX = true;
    }

    public override Vector2 InitialSize => new Vector2(360f, 150f);

    private bool Valid => !name.Trim().NullOrEmpty();

    public override void DoWindowContents(Rect inRect)
    {
        Text.Font = GameFont.Small;
        Widgets.Label(new Rect(inRect.x, inRect.y, inRect.width, 26f), "VUIP.MainBarMenuNamePrompt".Translate());
        GUI.SetNextControlName("VUIP.MainBarMenuName");
        name = Widgets.TextField(new Rect(inRect.x, inRect.y + 30f, inRect.width, 30f), name);
        if (name.Length > MaxNameLength)
        {
            name = name.Substring(0, MaxNameLength);
        }

        if (!focused)
        {
            UI.FocusControl("VUIP.MainBarMenuName", this);
            focused = true;
        }

        Rect buttons = new Rect(inRect.x, inRect.yMax - 32f, inRect.width, 32f);
        if (Widgets.ButtonText(buttons.LeftHalf().ContractedBy(4f, 0f), "Cancel".Translate()))
        {
            Close();
        }

        if (Widgets.ButtonText(buttons.RightHalf().ContractedBy(4f, 0f), "OK".Translate(), active: Valid))
        {
            OnAcceptKeyPressed();
        }
    }

    public override void OnAcceptKeyPressed()
    {
        if (!Valid)
        {
            return;
        }

        onAccept(name.Trim());
        Close();
    }
}
