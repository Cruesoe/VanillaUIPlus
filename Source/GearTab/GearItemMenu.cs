using System;
using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.Sound;

namespace VanillaUIPlus;

/// <summary>
/// What a piece of gear offers: the right-click menu, and the buttons that run the same actions. Unavailable actions are greyed out with the reason.
/// </summary>
public static class GearItemMenu
{
    public static void Open(ITab_Pawn_Gear tab, GearSnapshot snapshot, GearItem item)
    {
        Pawn pawn = snapshot.Pawn;
        Thing thing = item.Thing;
        List<FloatMenuOption> options = new List<FloatMenuOption>
        {
            new FloatMenuOption("ThingInfo".Translate(), () => Find.WindowStack.Add(new Dialog_InfoCard(thing)))
        };

        if (item.CanForce)
        {
            Apparel apparel = (Apparel)thing;
            string label = (item.Forced ? "VUIP.GearUnforce" : "VUIP.GearForce").Translate();
            options.Add(new FloatMenuOption(label, () => ToggleForced(pawn, apparel)));
        }

        if (item.Action != null)
        {
            GearRowAction action = item.Action;
            string label = ActionLabel(action.Kind);
            options.Add(action.Allowed
                ? new FloatMenuOption(label, () => Perform(pawn, thing, action))
                : new FloatMenuOption(label + ": " + action.Reason, null));
        }

        if (item.CanEat)
        {
            options.Add(new FloatMenuOption("ConsumeThing".Translate(thing.LabelNoCount, thing), () => Eat(pawn, thing)));
        }

        if (item.DropShown)
        {
            string label = "DropThing".Translate();
            options.Add(item.DropBlocked == null
                ? new FloatMenuOption(label, () => Drop(tab, pawn, thing))
                : new FloatMenuOption(label + ": " + item.DropBlocked, null));
        }

        Find.WindowStack.Add(new FloatMenu(options, thing.LabelCap));
    }

    public static string ActionLabel(GearRowAction.ActionKind kind)
    {
        return kind switch
        {
            GearRowAction.ActionKind.RemoveToInventory => "VUIP.GearRemoveLabel".Translate(),
            GearRowAction.ActionKind.WearFromInventory => "VUIP.GearWearLabel".Translate(),
            GearRowAction.ActionKind.UnequipToInventory => "VUIP.GearUnequipLabel".Translate(),
            _ => "VUIP.GearEquipLabel".Translate(),
        };
    }

    public static void Perform(Pawn pawn, Thing thing, GearRowAction action)
    {
        switch (action.Kind)
        {
            case GearRowAction.ActionKind.RemoveToInventory:
                GearActions.OrderRemoveToInventory(pawn, (Apparel)thing);
                break;
            case GearRowAction.ActionKind.WearFromInventory:
                GearActions.OrderWearFromInventory(pawn, (Apparel)thing);
                break;
            case GearRowAction.ActionKind.UnequipToInventory:
                GearActions.UnequipToInventory(pawn, (ThingWithComps)thing);
                break;
            case GearRowAction.ActionKind.EquipFromInventory:
                GearActions.EquipFromInventory(pawn, thing);
                break;
        }
    }

    // The base game's drop button: confirms lost mech bandwidth first.
    public static void Drop(ITab_Pawn_Gear tab, Pawn pawn, Thing thing)
    {
        Action action = () =>
        {
            SoundDefOf.Tick_High.PlayOneShotOnCamera();
            GearTabAccess.InterfaceDrop(tab, thing);
            GearSnapshot.Invalidate();
        };
        if (!ModsConfig.BiotechActive || !MechanitorUtility.TryConfirmBandwidthLossFromDroppingThing(pawn, thing, action))
        {
            action();
        }
    }

    public static void Eat(Pawn pawn, Thing thing)
    {
        SoundDefOf.Tick_High.PlayOneShotOnCamera();
        FoodUtility.IngestFromInventoryNow(pawn, thing);
    }

    public static void ToggleForced(Pawn pawn, Apparel apparel)
    {
        if (pawn.outfits == null)
        {
            return;
        }

        pawn.outfits.forcedHandler.SetForced(apparel, !pawn.outfits.forcedHandler.IsForced(apparel));
        GearSnapshot.Invalidate();
    }
}
