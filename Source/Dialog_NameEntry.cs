using UnityEngine;
using Verse;

namespace VanillaUIPlus;

/// <summary>A small window that asks for a name: a prompt, a focused text field, Cancel and an accept button.</summary>
public abstract class Dialog_NameEntry : Window
{
    private const int MaxNameLength = 40;
    private string name;
    private bool focused;

    protected Dialog_NameEntry(string name)
    {
        this.name = name;
        forcePause = false;
        closeOnClickedOutside = true;
        absorbInputAroundWindow = true;
        doCloseX = true;
    }

    public override Vector2 InitialSize => new Vector2(360f, 150f);

    protected abstract string Prompt { get; }

    protected abstract string ControlName { get; }

    protected virtual string AcceptLabel(string trimmedName) => "OK".Translate();

    protected abstract void Accept(string trimmedName);

    private bool Valid => !name.Trim().NullOrEmpty();

    public override void DoWindowContents(Rect inRect)
    {
        Text.Font = GameFont.Small;
        Widgets.Label(new Rect(inRect.x, inRect.y, inRect.width, 26f), Prompt);
        GUI.SetNextControlName(ControlName);
        name = Widgets.TextField(new Rect(inRect.x, inRect.y + 30f, inRect.width, 30f), name);
        if (name.Length > MaxNameLength)
        {
            name = name.Substring(0, MaxNameLength);
        }

        if (!focused)
        {
            UI.FocusControl(ControlName, this);
            focused = true;
        }

        Rect buttons = new Rect(inRect.x, inRect.yMax - 32f, inRect.width, 32f);
        if (Widgets.ButtonText(buttons.LeftHalf().ContractedBy(4f, 0f), "Cancel".Translate()))
        {
            Close();
        }

        if (Widgets.ButtonText(buttons.RightHalf().ContractedBy(4f, 0f), AcceptLabel(name.Trim()), active: Valid))
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

        Accept(name.Trim());
        Close();
    }
}
