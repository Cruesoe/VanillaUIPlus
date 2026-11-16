using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using Verse;

namespace VanillaUIPlus;

/// <summary>
/// Combat Extended's Gear tab members (weight and bulk, held items, ammo, loadouts, attachments, drop and haul), through reflection.
/// Each part is optional: a missing member drops only that part, and the tab falls back to the base game's behavior for it.
/// </summary>
public static class CombatExtendedGear
{
    private static bool resolved;

    // CompInventory: weight and bulk carried and their capacities, and CE's weapon switch.
    private static Type? inventoryType;
    private static Func<object, float>? currentWeight;
    private static Func<object, float>? capacityWeight;
    private static Func<object, float>? currentBulk;
    private static Func<object, float>? capacityBulk;
    private static MethodInfo? trySwitchToWeapon;
    private static Func<Pawn, string>? weightTip;
    private static Func<Pawn, string>? bulkTip;
    private static StatDef? bulkStat;

    // Items the pawn keeps in its inventory regardless of loadout (Utility_HoldTracker).
    private static Func<Pawn, Thing, bool>? isHeld;
    private static Action<Pawn, Thing>? forgetHeld;

    // CompAmmoUser: the loaded magazine and ammo type.
    private static Type? ammoType;
    private static Func<object, bool>? useAmmo;
    private static Func<object, bool>? hasMagazine;
    private static Func<object, int>? magazineCount;
    private static Func<object, int>? magazineSize;
    private static PropertyInfo? currentAmmo;

    private static MethodInfo? generateLoadout;
    private static MethodInfo? addLoadout;
    private static MethodInfo? dropHaul;
    private static Type? platformType;
    private static Type? attachmentsEditorType;

    /// <summary>Whether Combat Extended is active; each part below also checks its own members.</summary>
    public static bool Active
    {
        get
        {
            if (!PawnReadout.CombatExtendedActive)
            {
                return false;
            }

            Resolve();
            return true;
        }
    }

    // CE's weight and bulk for the pawn; false without a CompInventory (or when its members weren't found).
    public static bool TryGetLoad(Pawn pawn, out float weight, out float weightCapacity, out float bulk, out float bulkCapacity)
    {
        weight = weightCapacity = bulk = bulkCapacity = 0f;
        if (!Active || currentWeight == null || capacityWeight == null || currentBulk == null || capacityBulk == null)
        {
            return false;
        }

        ThingComp? comp = Comp(pawn, inventoryType);
        if (comp == null)
        {
            return false;
        }

        weight = currentWeight(comp);
        weightCapacity = capacityWeight(comp);
        bulk = currentBulk(comp);
        bulkCapacity = capacityBulk(comp);
        return true;
    }

    public static string WeightTip(Pawn pawn) => weightTip != null ? Safe(() => weightTip(pawn)) : string.Empty;

    public static string BulkTip(Pawn pawn) => bulkTip != null ? Safe(() => bulkTip(pawn)) : string.Empty;

    // The item stack's bulk, or -1 when CE's Bulk stat isn't loaded.
    public static float Bulk(Thing thing)
    {
        return Active && bulkStat != null ? thing.GetStatValue(bulkStat) * thing.stackCount : -1f;
    }

    public static bool IsHeld(Pawn pawn, Thing thing) => Active && isHeld != null && Safe(() => isHeld(pawn, thing));

    public static bool CanForgetHeld => forgetHeld != null;

    public static void ForgetHeld(Pawn pawn, Thing thing)
    {
        forgetHeld?.Invoke(pawn, thing);
        GearSnapshot.Invalidate();
    }

    // The weapon's loaded magazine and ammo type; false for weapons without CE ammo.
    public static bool TryGetAmmo(Thing weapon, out int loaded, out int capacity, out Def? ammo)
    {
        loaded = capacity = 0;
        ammo = null;
        if (!Active || useAmmo == null || hasMagazine == null || magazineCount == null || magazineSize == null)
        {
            return false;
        }

        ThingComp? comp = weapon is ThingWithComps withComps ? Comp(withComps, ammoType) : null;
        if (comp == null || !hasMagazine(comp))
        {
            return false;
        }

        loaded = magazineCount(comp);
        capacity = magazineSize(comp);
        ammo = useAmmo(comp) ? currentAmmo?.GetValue(comp) as Def : null;
        return true;
    }

    public static bool CanSwitchWeapon(Pawn pawn) => Active && trySwitchToWeapon != null && Comp(pawn, inventoryType) != null;

    // CE's own equip from the inventory, which keeps its ammo and loadout bookkeeping.
    public static void SwitchToWeapon(Pawn pawn, ThingWithComps weapon)
    {
        ThingComp? comp = Comp(pawn, inventoryType);
        if (comp != null && trySwitchToWeapon != null)
        {
            trySwitchToWeapon.Invoke(comp, new object[] { weapon, true });
            GearSnapshot.Invalidate();
        }
    }

    public static bool CanMakeLoadout => generateLoadout != null && addLoadout != null && CombatExtendedLoadouts.Available;

    // A new loadout from the pawn's current gear, assigned to it and opened in CE's dialog, as CE's Make loadout link does.
    public static void MakeLoadout(Pawn pawn)
    {
        object? loadout = generateLoadout?.Invoke(null, new object[] { pawn });
        if (loadout == null)
        {
            return;
        }

        addLoadout!.Invoke(null, new[] { loadout });
        CombatExtendedLoadouts.Assign(pawn, loadout);
        if (CombatExtendedLoadouts.ManageDialog(loadout) is { } dialog)
        {
            Find.WindowStack.Add(dialog);
        }

        GearSnapshot.Invalidate();
    }

    // CE's drop that hauls the item to storage; only on CE's own Gear tab.
    public static bool CanDropHaul(ITab_Pawn_Gear tab) => Active && dropHaul != null && dropHaul.DeclaringType!.IsInstanceOfType(tab);

    public static void DropHaul(ITab_Pawn_Gear tab, Thing thing)
    {
        dropHaul!.Invoke(tab, new object[] { thing });
        GearSnapshot.Invalidate();
    }

    public static bool HasAttachments(Thing thing) => Active && platformType != null && attachmentsEditorType != null && platformType.IsInstanceOfType(thing);

    public static void OpenAttachments(Thing weapon)
    {
        IList<Window> windows = Find.WindowStack.Windows;
        for (int i = 0; i < windows.Count; i++)
        {
            if (attachmentsEditorType!.IsInstanceOfType(windows[i]))
            {
                return;
            }
        }

        try
        {
            if (Activator.CreateInstance(attachmentsEditorType!, weapon) is Window window)
            {
                Find.WindowStack.Add(window);
            }
        }
        catch (Exception exception)
        {
            Log.Warning("[Vanilla UI+] Could not open Combat Extended's attachments editor.\n" + exception);
        }
    }

    private static ThingComp? Comp(ThingWithComps thing, Type? type)
    {
        if (type == null)
        {
            return null;
        }

        List<ThingComp> comps = thing.AllComps;
        for (int i = 0; i < comps.Count; i++)
        {
            if (type.IsInstanceOfType(comps[i]))
            {
                return comps[i];
            }
        }

        return null;
    }

    private static T Safe<T>(Func<T> call)
    {
        try
        {
            return call();
        }
        catch (Exception)
        {
            return default!;
        }
    }

    private static void Resolve()
    {
        if (resolved)
        {
            return;
        }

        resolved = true;
        inventoryType = AccessTools.TypeByName("CombatExtended.CompInventory");
        if (inventoryType != null)
        {
            currentWeight = Getter<float>(inventoryType, "currentWeight");
            capacityWeight = Getter<float>(inventoryType, "capacityWeight");
            currentBulk = Getter<float>(inventoryType, "currentBulk");
            capacityBulk = Getter<float>(inventoryType, "capacityBulk");
            trySwitchToWeapon = AccessTools.Method(inventoryType, "TrySwitchToWeapon", new[] { typeof(ThingWithComps), typeof(bool) });
            ReflectionGuard.Found("CompInventory", "TrySwitchToWeapon", trySwitchToWeapon);
        }

        Type? loadouts = AccessTools.TypeByName("CombatExtended.Utility_Loadouts");
        if (loadouts != null)
        {
            weightTip = ReflectionGuard.Delegate<Func<Pawn, string>>("Utility_Loadouts", "GetWeightTip", AccessTools.Method(loadouts, "GetWeightTip", new[] { typeof(Pawn) }));
            bulkTip = ReflectionGuard.Delegate<Func<Pawn, string>>("Utility_Loadouts", "GetBulkTip", AccessTools.Method(loadouts, "GetBulkTip", new[] { typeof(Pawn) }));
            generateLoadout = AccessTools.Method(loadouts, "GenerateLoadoutFromPawn", new[] { typeof(Pawn) });
            ReflectionGuard.Found("Utility_Loadouts", "GenerateLoadoutFromPawn", generateLoadout);
        }

        Type? manager = AccessTools.TypeByName("CombatExtended.LoadoutManager");
        Type? loadoutType = AccessTools.TypeByName("CombatExtended.Loadout");
        addLoadout = manager == null || loadoutType == null ? null : AccessTools.Method(manager, "AddLoadout", new[] { loadoutType });

        Type? hold = AccessTools.TypeByName("CombatExtended.Utility_HoldTracker");
        if (hold != null)
        {
            isHeld = ReflectionGuard.Delegate<Func<Pawn, Thing, bool>>("Utility_HoldTracker", "HoldTrackerIsHeld",
                AccessTools.Method(hold, "HoldTrackerIsHeld", new[] { typeof(Pawn), typeof(Thing) }));
            forgetHeld = ReflectionGuard.Delegate<Action<Pawn, Thing>>("Utility_HoldTracker", "HoldTrackerForget",
                AccessTools.Method(hold, "HoldTrackerForget", new[] { typeof(Pawn), typeof(Thing) }));
        }

        ammoType = AccessTools.TypeByName("CombatExtended.CompAmmoUser");
        if (ammoType != null)
        {
            useAmmo = Getter<bool>(ammoType, "UseAmmo");
            hasMagazine = Getter<bool>(ammoType, "HasMagazine");
            magazineCount = Getter<int>(ammoType, "CurMagCount");
            magazineSize = Getter<int>(ammoType, "MagSize");
            currentAmmo = AccessTools.Property(ammoType, "CurrentAmmo");
        }

        Type? tab = AccessTools.TypeByName("CombatExtended.ITab_Inventory");
        dropHaul = tab == null ? null : AccessTools.Method(tab, "InterfaceDropHaul", new[] { typeof(Thing) });
        platformType = AccessTools.TypeByName("CombatExtended.WeaponPlatform");
        attachmentsEditorType = AccessTools.TypeByName("CombatExtended.Window_AttachmentsEditor");
        bulkStat = DefDatabase<StatDef>.GetNamedSilentFail("Bulk");
    }

    private static Func<object, T>? Getter<T>(Type type, string property)
    {
        return ReflectionGuard.UntypedDelegate<T>(type.Name, property, AccessTools.PropertyGetter(type, property));
    }
}
