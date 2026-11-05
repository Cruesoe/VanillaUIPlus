using System;
using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;
using Verse.Sound;

namespace VanillaUIPlus;

/// <summary>
/// Moving gear between a pawn's body and inventory. Each check returns null when the action is allowed, otherwise the reason it isn't.
/// </summary>
public static class GearActions
{
    public static string? CannotRemoveToInventory(Pawn pawn, Apparel apparel)
    {
        if (pawn.apparel.IsLocked(apparel))
        {
            return "DropThingLocked".Translate();
        }

        if (pawn.IsQuestLodger() && !EquipmentUtility.QuestLodgerCanUnequip(apparel, pawn))
        {
            return "DropThingLodger".Translate();
        }

        return null;
    }

    public static string? CannotWearFromInventory(Pawn pawn, Apparel apparel)
    {
        if (pawn.apparel.WouldReplaceLockedApparel(apparel))
        {
            return "WouldReplaceLockedApparel".Translate().CapitalizeFirst();
        }

        if (pawn.IsMutant && pawn.mutant.Def.disableApparel)
        {
            return pawn.mutant.Def.LabelCap;
        }

        if (!ApparelUtility.HasPartsToWear(pawn, apparel.def))
        {
            return "CannotWearBecauseOfMissingBodyParts".Translate().CapitalizeFirst();
        }

        if (!EquipmentUtility.CanEquip(apparel, pawn, out string reason))
        {
            return reason.CapitalizeFirst();
        }

        // Pawn_ApparelTracker.Wear refuses these without a reason, after the item has left the inventory.
        if (!apparel.PawnCanWear(pawn, ignoreGender: true))
        {
            return "VUIP.GearCannotWear".Translate();
        }

        return null;
    }

    public static string? CannotUnequipToInventory(Pawn pawn, ThingWithComps weapon)
    {
        if (pawn.IsQuestLodger() && !EquipmentUtility.QuestLodgerCanUnequip(weapon, pawn))
        {
            return "DropThingLodger".Translate();
        }

        return null;
    }

    public static string? CannotEquipFromInventory(Pawn pawn, Thing weapon)
    {
        if (pawn.equipment == null)
        {
            return "Incapable".Translate().CapitalizeFirst();
        }

        if (weapon.def.IsWeapon && pawn.WorkTagIsDisabled(WorkTags.Violent))
        {
            return "IsIncapableOfViolenceLower".Translate(pawn.LabelShort, pawn).CapitalizeFirst();
        }

        if (weapon.def.IsRangedWeapon && pawn.WorkTagIsDisabled(WorkTags.Shooting))
        {
            return "IsIncapableOfShootingLower".Translate(pawn).CapitalizeFirst();
        }

        if (!pawn.health.capacities.CapableOf(PawnCapacityDefOf.Manipulation))
        {
            return "Incapable".Translate().CapitalizeFirst();
        }

        if (pawn.IsQuestLodger() && !EquipmentUtility.QuestLodgerCanEquip(weapon, pawn))
        {
            return "QuestRelated".Translate().CapitalizeFirst();
        }

        if (!EquipmentUtility.CanEquip(weapon, pawn, out string reason, checkBonded: false))
        {
            return reason.CapitalizeFirst();
        }

        if (pawn.equipment.Primary != null && CannotUnequipToInventory(pawn, pawn.equipment.Primary) is { } primaryReason)
        {
            return primaryReason;
        }

        return null;
    }

    /// <summary>Orders the pawn to take the apparel off into its inventory, confirming lost mech bandwidth first.</summary>
    public static void OrderRemoveToInventory(Pawn pawn, Apparel apparel)
    {
        Action action = () =>
        {
            SoundDefOf.Tick_High.PlayOneShotOnCamera();
            pawn.jobs.TryTakeOrderedJob(JobMaker.MakeJob(VUIPDefOf.VUIP_RemoveApparelToInventory, apparel), JobTag.Misc);
        };
        if (!ModsConfig.BiotechActive || !MechanitorUtility.TryConfirmBandwidthLossFromDroppingThing(pawn, apparel, action))
        {
            action();
        }
    }

    /// <summary>Orders the pawn to put on apparel from its inventory, confirming lost mech bandwidth from what it replaces first.</summary>
    public static void OrderWearFromInventory(Pawn pawn, Apparel apparel)
    {
        Action action = () =>
        {
            SoundDefOf.Tick_High.PlayOneShotOnCamera();
            pawn.jobs.TryTakeOrderedJob(JobMaker.MakeJob(VUIPDefOf.VUIP_WearFromInventory, apparel), JobTag.Misc);
        };
        Apparel? replaced = ApparelUtility.GetApparelReplacedByNewApparel(pawn, apparel);
        if (replaced == null || !ModsConfig.BiotechActive || !MechanitorUtility.TryConfirmBandwidthLossFromDroppingThing(pawn, replaced, action))
        {
            action();
        }
    }

    /// <summary>Orders the pawn to put on apparel from the map, as the base game's right-click Wear does.</summary>
    public static void OrderWearFromMap(Pawn pawn, Apparel apparel)
    {
        Action action = () =>
        {
            apparel.SetForbidden(false);
            pawn.jobs.TryTakeOrderedJob(JobMaker.MakeJob(JobDefOf.Wear, apparel), JobTag.Misc);
        };
        Apparel? replaced = ApparelUtility.GetApparelReplacedByNewApparel(pawn, apparel);
        if (replaced == null || !ModsConfig.BiotechActive || !MechanitorUtility.TryConfirmBandwidthLossFromDroppingThing(pawn, replaced, action))
        {
            action();
        }
    }

    /// <summary>Moves the weapon into the inventory at once, as dropping one does.</summary>
    public static void UnequipToInventory(Pawn pawn, ThingWithComps weapon)
    {
        if (pawn.equipment.TryTransferEquipmentToContainer(weapon, pawn.inventory.innerContainer))
        {
            SoundDefOf.Tick_High.PlayOneShotOnCamera();
            GearSnapshot.Invalidate();
        }
    }

    /// <summary>Equips a weapon from the inventory at once, as the base game's equip does on arrival; the current weapon goes into the inventory.</summary>
    public static void EquipFromInventory(Pawn pawn, Thing weapon)
    {
        string confirmation = EquipmentUtility.GetPersonaWeaponConfirmationText(weapon, pawn);
        if (!confirmation.NullOrEmpty())
        {
            Find.WindowStack.Add(new Dialog_MessageBox(confirmation, "Yes".Translate(), () => DoEquipFromInventory(pawn, weapon), "No".Translate()));
            return;
        }

        DoEquipFromInventory(pawn, weapon);
    }

    private static void DoEquipFromInventory(Pawn pawn, Thing weapon)
    {
        if (!pawn.inventory.innerContainer.Contains(weapon))
        {
            return;
        }

        ThingWithComps? primary = pawn.equipment.Primary;
        if (primary != null && !pawn.equipment.TryTransferEquipmentToContainer(primary, pawn.inventory.innerContainer))
        {
            return;
        }

        if (weapon.SplitOff(1) is not ThingWithComps equipped)
        {
            return;
        }

        pawn.equipment.AddEquipment(equipped);
        equipped.def.soundInteract?.PlayOneShot(new TargetInfo(pawn.PositionHeld, pawn.MapHeld));
        GearSnapshot.Invalidate();
    }

    /// <summary>Takes worn apparel off into the inventory; dropped near the pawn if the inventory won't take it.</summary>
    public static bool MoveWornToInventory(Pawn pawn, Apparel apparel)
    {
        if (!pawn.apparel.WornApparel.Contains(apparel))
        {
            return false;
        }

        pawn.apparel.Remove(apparel);
        AddToInventoryOrDrop(pawn, apparel);
        GearSnapshot.Invalidate();
        return true;
    }

    /// <summary>Puts on apparel from the inventory; what it replaces goes into the inventory instead of onto the ground.</summary>
    public static bool WearFromInventory(Pawn pawn, Apparel apparel, bool forced)
    {
        if (!pawn.inventory.innerContainer.Contains(apparel) || CannotWearFromInventory(pawn, apparel) != null)
        {
            return false;
        }

        pawn.inventory.innerContainer.Remove(apparel);
        List<Apparel> worn = pawn.apparel.WornApparel;
        for (int i = worn.Count - 1; i >= 0; i--)
        {
            Apparel current = worn[i];
            if (!ApparelUtility.CanWearTogether(apparel.def, current.def, pawn.RaceProps.body))
            {
                pawn.apparel.Remove(current);
                AddToInventoryOrDrop(pawn, current);
            }
        }

        pawn.apparel.Wear(apparel, dropReplacedApparel: false);
        if (forced && pawn.outfits != null)
        {
            pawn.outfits.forcedHandler.SetForced(apparel, forced: true);
        }

        GearSnapshot.Invalidate();
        return true;
    }

    private static void AddToInventoryOrDrop(Pawn pawn, Thing thing)
    {
        if (!pawn.inventory.innerContainer.TryAdd(thing, canMergeWithExistingStacks: false) && pawn.MapHeld != null)
        {
            GenPlace.TryPlaceThing(thing, pawn.PositionHeld, pawn.MapHeld, ThingPlaceMode.Near);
        }
    }
}
