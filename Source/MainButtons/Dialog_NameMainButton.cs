using System;
using Verse;

namespace VanillaUIPlus;

public sealed class Dialog_NameMainButton : Dialog_NameEntry
{
    private readonly string promptKey;
    private readonly Action<string> onAccept;

    public Dialog_NameMainButton(string name, string promptKey, Action<string> onAccept) : base(name)
    {
        this.promptKey = promptKey;
        this.onAccept = onAccept;
    }

    protected override string Prompt => promptKey.Translate();

    protected override string ControlName => "VUIP.MainBarName";

    protected override void Accept(string trimmedName) => onAccept(trimmedName);
}
