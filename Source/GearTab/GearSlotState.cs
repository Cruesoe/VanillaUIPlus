using System.Collections.Generic;

namespace VanillaUIPlus;

/// <summary>One slot as shown for a pawn: what fills it and, if it's a problem, why.</summary>
public sealed class GearSlotState
{
    public readonly GearSlot Slot;
    // Worn apparel in this slot; the first is drawn, the rest counted. One item can fill several slots (a jumpsuit fills shirt and legs).
    public readonly List<GearItem> Items;
    public readonly string? Problem;
    public readonly string Tip;
    public readonly string ClickTip;
    public readonly string MoreText;

    public GearSlotState(GearSlot slot, List<GearItem> items, string? problem, string tip, string clickTip)
    {
        Slot = slot;
        Items = items;
        Problem = problem;
        Tip = tip;
        ClickTip = clickTip;
        MoreText = items.Count > 1 ? "+" + (items.Count - 1) : string.Empty;
    }

    public GearItem? Item => Items.Count > 0 ? Items[0] : null;
}
