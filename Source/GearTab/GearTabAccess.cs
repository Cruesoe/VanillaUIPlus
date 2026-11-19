using System;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace VanillaUIPlus;

/// <summary>
/// The Gear tab's private members, called through delegates so other mods' patches on them still run.
/// </summary>
public static class GearTabAccess
{
    public delegate void DrawThingRowCall(ITab_Pawn_Gear tab, ref float y, float width, Thing thing, bool inventory);

    private const string Owner = "ITab_Pawn_Gear";

    private static bool resolved;
    private static Func<ITab_Pawn_Gear, Pawn>? selPawnForGear;
    private static Func<ITab_Pawn_Gear, bool>? canControl;
    private static Func<ITab_Pawn_Gear, bool>? canControlColonist;
    private static Func<ITab_Pawn_Gear, Pawn, bool>? shouldShowInventory;
    private static Func<ITab_Pawn_Gear, Pawn, bool>? shouldShowApparel;
    private static Func<ITab_Pawn_Gear, Pawn, bool>? shouldShowEquipment;
    private static Func<ITab_Pawn_Gear, Pawn, bool>? shouldShowOverallArmor;
    private static DrawThingRowCall? drawThingRow;
    private static AccessTools.FieldRef<InspectTabBase, Vector2>? size;

    /// <summary>Whether every member was found; otherwise the base game's tab is drawn.</summary>
    public static bool Available
    {
        get
        {
            Resolve();
            return selPawnForGear != null && canControl != null && canControlColonist != null && shouldShowInventory != null
                && shouldShowApparel != null && shouldShowEquipment != null && shouldShowOverallArmor != null && drawThingRow != null && size != null;
        }
    }

    public static Pawn SelPawnForGear(ITab_Pawn_Gear tab) => selPawnForGear!(tab);

    public static bool CanControl(ITab_Pawn_Gear tab) => canControl!(tab);

    public static bool CanControlColonist(ITab_Pawn_Gear tab) => canControlColonist!(tab);

    public static bool ShouldShowInventory(ITab_Pawn_Gear tab, Pawn pawn) => shouldShowInventory!(tab, pawn);

    public static bool ShouldShowApparel(ITab_Pawn_Gear tab, Pawn pawn) => shouldShowApparel!(tab, pawn);

    public static bool ShouldShowEquipment(ITab_Pawn_Gear tab, Pawn pawn) => shouldShowEquipment!(tab, pawn);

    public static bool ShouldShowOverallArmor(ITab_Pawn_Gear tab, Pawn pawn) => shouldShowOverallArmor!(tab, pawn);

    public static void DrawThingRow(ITab_Pawn_Gear tab, ref float y, float width, Thing thing, bool inventory) => drawThingRow!(tab, ref y, width, thing, inventory);

    public static ref Vector2 Size(ITab_Pawn_Gear tab) => ref size!(tab);

    private static void Resolve()
    {
        if (resolved)
        {
            return;
        }

        resolved = true;
        Type type = typeof(ITab_Pawn_Gear);
        selPawnForGear = ReflectionGuard.Delegate<Func<ITab_Pawn_Gear, Pawn>>(Owner, "SelPawnForGear", AccessTools.PropertyGetter(type, "SelPawnForGear"));
        canControl = ReflectionGuard.Delegate<Func<ITab_Pawn_Gear, bool>>(Owner, "CanControl", AccessTools.PropertyGetter(type, "CanControl"));
        canControlColonist = ReflectionGuard.Delegate<Func<ITab_Pawn_Gear, bool>>(Owner, "CanControlColonist", AccessTools.PropertyGetter(type, "CanControlColonist"));
        shouldShowInventory = ShouldShow("ShouldShowInventory");
        shouldShowApparel = ShouldShow("ShouldShowApparel");
        shouldShowEquipment = ShouldShow("ShouldShowEquipment");
        shouldShowOverallArmor = ShouldShow("ShouldShowOverallArmor");
        drawThingRow = ReflectionGuard.Delegate<DrawThingRowCall>(Owner, "DrawThingRow",
            AccessTools.Method(type, "DrawThingRow", new[] { typeof(float).MakeByRefType(), typeof(float), typeof(Thing), typeof(bool) }));
        size = ReflectionGuard.FieldRef<InspectTabBase, Vector2>("InspectTabBase", "size", AccessTools.Field(typeof(InspectTabBase), "size"));
    }

    private static Func<ITab_Pawn_Gear, Pawn, bool>? ShouldShow(string name)
    {
        return ReflectionGuard.Delegate<Func<ITab_Pawn_Gear, Pawn, bool>>(Owner, name, AccessTools.Method(typeof(ITab_Pawn_Gear), name, new[] { typeof(Pawn) }));
    }
}
