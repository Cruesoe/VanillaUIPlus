using System;
using Verse;

namespace VanillaUIPlus;

public sealed class Dialog_NameMainButtonMenu : Dialog_NameEntry
{
    private readonly Action<string> onAccept;

    public Dialog_NameMainButtonMenu(string name, Action<string> onAccept) : base(name)
    {
        this.onAccept = onAccept;
    }

    protected override string Prompt => "VUIP.MainBarMenuNamePrompt".Translate();

    protected override string ControlName => "VUIP.MainBarMenuName";

    protected override void Accept(string trimmedName) => onAccept(trimmedName);
}
