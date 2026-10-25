using System;
using Verse;

namespace VanillaUIPlus;

public sealed class Dialog_NameMainButtonTab : Dialog_NameEntry
{
    private readonly Action<string> onAccept;

    public Dialog_NameMainButtonTab(string name, Action<string> onAccept) : base(name)
    {
        this.onAccept = onAccept;
    }

    protected override string Prompt => "VUIP.MainBarTabNamePrompt".Translate();

    protected override string ControlName => "VUIP.MainBarTabName";

    protected override void Accept(string trimmedName) => onAccept(trimmedName);
}
