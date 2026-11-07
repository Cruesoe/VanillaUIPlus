using Verse;

namespace VanillaUIPlus;

/// <summary>The move-to or move-from-inventory button on one Gear tab row, with its tooltip and, if it can't be used, why.</summary>
public sealed class GearRowAction
{
    public enum ActionKind
    {
        RemoveToInventory,
        WearFromInventory,
        UnequipToInventory,
        EquipFromInventory
    }

    public readonly ActionKind Kind;
    public readonly string? Reason;
    public readonly string Tip;

    public GearRowAction(ActionKind kind, string? reason, string tip)
    {
        Kind = kind;
        Reason = reason;
        Tip = reason == null ? tip : tip + "\n\n" + reason.Colorize(ColorLibrary.RedReadable);
    }

    public bool Allowed => Reason == null;
}
