using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace VanillaUIPlus;

/// <summary>
/// Grows the Bio tab until its left column fits without a scrollbar: each draw measures the sections and stores the missing height per pawn.
/// </summary>
public static class CharacterTabSize
{
    // Space under the first section when the column overflows, which Progression Education always triggers.
    private const float FirstSectionGap = 22f;
    private const float Margin = 1f;
    // Room left for the bottom bar, tab row and inspect pane, which the tab sits on top of.
    private static float ReservedScreenHeight => PawnReadout.CurrentPaneHeight() + 75f;

    private static readonly Dictionary<int, float> ExtraByPawn = new Dictionary<int, float>();
    // Reads one section's rect height straight from the game's List<LeftRectSection>, without boxing the struct.
    private static readonly Func<object, int, float>? SectionHeight = BuildSectionHeight();

    private static IList? sections;

    public static Pawn? DrawingPawn;

    public static Vector2 Expand(Vector2 size)
    {
        return Expand(size, DrawingPawn);
    }

    public static Vector2 Expand(Vector2 size, Pawn? pawn)
    {
        if (pawn != null && ExtraByPawn.TryGetValue(pawn.thingIDNumber, out float extra))
        {
            size.y += Mathf.Min(extra, Mathf.Max(0f, UI.screenHeight - ReservedScreenHeight - size.y));
        }

        return size;
    }

    public static void RememberSections(object list)
    {
        sections = list as IList;
    }

    public static void Measure(Rect leftRect, Pawn pawn)
    {
        IList? measured = sections;
        sections = null;
        if (DrawingPawn != pawn || measured == null || measured.Count == 0 || SectionHeight == null)
        {
            return;
        }

        float needed = FirstSectionGap + Margin;
        for (int i = 0; i < measured.Count; i++)
        {
            needed += SectionHeight(measured, i);
        }

        ExtraByPawn.TryGetValue(pawn.thingIDNumber, out float extra);
        float updated = Mathf.Clamp(Mathf.Ceil(extra + needed - leftRect.height), 0f, UI.screenHeight);
        if (updated == extra)
        {
            return;
        }

        if (ExtraByPawn.Count > 200)
        {
            ExtraByPawn.Clear();
        }

        ExtraByPawn[pawn.thingIDNumber] = updated;
    }

    public static bool CanMeasure => SectionHeight != null;

    private static Func<object, int, float>? BuildSectionHeight()
    {
        Type? section = AccessTools.Inner(typeof(CharacterCardUtility), "LeftRectSection");
        FieldInfo? rectField = section == null ? null : AccessTools.Field(section, "rect");
        if (!ReflectionGuard.Found(nameof(CharacterCardUtility), "LeftRectSection.rect", rectField) || rectField!.FieldType != typeof(Rect))
        {
            return null;
        }

        try
        {
            Type listType = typeof(List<>).MakeGenericType(section!);
            DynamicMethod method = new DynamicMethod("VUIP_SectionHeight", typeof(float), new[] { typeof(object), typeof(int) },
                typeof(CharacterTabSize).Module, skipVisibility: true);
            ILGenerator il = method.GetILGenerator();
            LocalBuilder item = il.DeclareLocal(section!);
            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Castclass, listType);
            il.Emit(OpCodes.Ldarg_1);
            il.Emit(OpCodes.Callvirt, AccessTools.PropertyGetter(listType, "Item"));
            il.Emit(OpCodes.Stloc, item);
            il.Emit(OpCodes.Ldloca, item);
            il.Emit(OpCodes.Ldflda, rectField);
            il.Emit(OpCodes.Call, AccessTools.PropertyGetter(typeof(Rect), nameof(Rect.height)));
            il.Emit(OpCodes.Ret);
            return (Func<object, int, float>)method.CreateDelegate(typeof(Func<object, int, float>));
        }
        catch (Exception exception)
        {
            Log.Warning($"[Vanilla UI+] Could not bind to the Bio tab's sections; the tab keeps its vanilla height.\n{exception}");
            return null;
        }
    }

    public static void Forget()
    {
        ExtraByPawn.Clear();
        sections = null;
        DrawingPawn = null;
    }

    // The pawn PawnToShowInfoAbout picks, without its error log when there is none.
    public static Pawn? SelectedPawn()
    {
        Thing? thing = Find.Selector.SingleSelectedThing;
        return thing as Pawn ?? (thing as Corpse)?.InnerPawn;
    }
}
