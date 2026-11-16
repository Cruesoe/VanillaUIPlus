using System.Collections.Generic;
using RimWorld;
using Verse;

namespace VanillaUIPlus;

/// <summary>
/// One piece of gear as the Gear tab shows it: labels, tooltip, health and what the player can do with it. Built with the snapshot.
/// </summary>
public sealed class GearItem
{
    public readonly Thing Thing;
    // Worn or equipped (false), or held in the inventory (true).
    public readonly bool Inventory;
    public string Label = string.Empty;
    // Muted second line under the label: a weapon's damage and range.
    public string Detail = string.Empty;
    public string Tip = string.Empty;
    // Stack size for an inventory cell; empty for a single item.
    public string CountText = string.Empty;
    // Hit points as a fraction, or -1 when the item has none.
    public float HitPoints = -1f;
    public string? Warning;
    public bool Forced;
    // Muted: a weapon carried in the inventory rather than equipped.
    public bool Muted;

    // Moving between body and inventory; null when not offered.
    public GearRowAction? Action;
    // The drop button: shown at all, and the reason it's blocked if it is.
    public bool DropShown;
    public string? DropBlocked;
    public string DropTip = string.Empty;
    public bool CanEat;
    public bool CanForce;

    // Combat Extended: kept in the inventory whatever the loadout says, the weapon's magazine and ammo for the tooltip,
    // worn apparel this stack can reload, and the attachments editor.
    public bool Held;
    public bool CanForgetHeld;
    public string AmmoText = string.Empty;
    public List<Apparel>? Reloads;
    public bool CanEditAttachments;

    public GearItem(Thing thing, bool inventory)
    {
        Thing = thing;
        Inventory = inventory;
    }
}
