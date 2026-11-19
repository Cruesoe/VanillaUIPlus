using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using Verse;

namespace VanillaUIPlus;

/// <summary>
/// Combat Extended's loadouts: a pawn's assigned loadout, the list to choose from and its manage dialog, through reflection.
/// </summary>
public static class CombatExtendedLoadouts
{
    private static bool resolved;
    // Bound once: the pawn pane reads the loadout and its label every frame.
    private static Func<Pawn, object>? getLoadout;
    private static AccessTools.FieldRef<object, string>? label;
    private static MethodInfo? setLoadout;
    private static PropertyInfo? loadouts;
    private static Type? dialogType;

    /// <summary>Whether Combat Extended is active and its loadout members were found.</summary>
    public static bool Available
    {
        get
        {
            if (!PawnReadout.CombatExtendedActive)
            {
                return false;
            }

            Resolve();
            return getLoadout != null && setLoadout != null && loadouts != null && label != null;
        }
    }

    // The pawn's loadout (CE assigns its default loadout on first read, as its Assign tab column does), or null.
    public static object? Loadout(Pawn pawn)
    {
        try
        {
            return getLoadout!(pawn);
        }
        catch (Exception)
        {
            return null;
        }
    }

    public static string Label(object loadout)
    {
        return label!(loadout) ?? string.Empty;
    }

    // Every loadout in CE's order, for the menu.
    public static IEnumerable<object> All()
    {
        if (loadouts!.GetValue(null) is IEnumerable list)
        {
            foreach (object each in list)
            {
                yield return each;
            }
        }
    }

    public static void Assign(Pawn pawn, object loadout)
    {
        setLoadout!.Invoke(null, new[] { pawn, loadout });
    }

    // CE's manage loadouts dialog opened on the given loadout, or null if it can't be built.
    public static Window? ManageDialog(object loadout)
    {
        try
        {
            return dialogType == null ? null : Activator.CreateInstance(dialogType, loadout) as Window;
        }
        catch (Exception)
        {
            return null;
        }
    }

    private static void Resolve()
    {
        if (resolved)
        {
            return;
        }

        resolved = true;
        Type? utility = AccessTools.TypeByName("CombatExtended.Utility_Loadouts");
        Type? loadoutType = AccessTools.TypeByName("CombatExtended.Loadout");
        Type? manager = AccessTools.TypeByName("CombatExtended.LoadoutManager");
        Type? column = AccessTools.TypeByName("CombatExtended.PawnColumnWorker_Loadout");
        MethodInfo? getter = utility == null ? null : AccessTools.Method(utility, "GetLoadout", new[] { typeof(Pawn) });
        FieldInfo? labelField = loadoutType == null ? null : AccessTools.Field(loadoutType, "label");
        try
        {
            getLoadout = getter == null ? null : AccessTools.MethodDelegate<Func<Pawn, object>>(getter);
            label = labelField == null ? null : AccessTools.FieldRefAccess<object, string>(labelField);
        }
        catch (Exception)
        {
            getLoadout = null;
            label = null;
        }

        // The Assign tab column's setter is synced for Multiplayer; the plain extension method is the fallback.
        setLoadout = column == null || loadoutType == null ? null : AccessTools.Method(column, "SetLoadout", new[] { typeof(Pawn), loadoutType });
        setLoadout ??= utility == null || loadoutType == null ? null : AccessTools.Method(utility, "SetLoadout", new[] { typeof(Pawn), loadoutType });
        loadouts = manager == null ? null : AccessTools.Property(manager, "Loadouts");
        dialogType = AccessTools.TypeByName("CombatExtended.Dialog_ManageLoadouts");
        if (getLoadout == null || setLoadout == null || loadouts == null || label == null)
        {
            Log.Warning("[Vanilla UI+] Combat Extended's loadout members were not found; the pawn pane's loadout bar is unavailable.");
        }
    }
}
