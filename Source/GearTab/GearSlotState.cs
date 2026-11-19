using RimWorld;

namespace VanillaUIPlus;

/// <summary>One slot as shown for a pawn: what fills it and, if it's a problem, why.</summary>
public sealed class GearSlotState
{
    public readonly GearSlot Slot;
    public readonly Apparel? Item;
    public readonly string? Problem;
    public readonly string Tip;
    public readonly string ClickTip;

    public GearSlotState(GearSlot slot, Apparel? item, string? problem, string tip, string clickTip)
    {
        Slot = slot;
        Item = item;
        Problem = problem;
        Tip = tip;
        ClickTip = clickTip;
    }
}
