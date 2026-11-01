using System.Collections;
using System.Collections.Generic;
using System.Reflection;
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
    private static readonly FieldInfo? SectionRectField =
        AccessTools.Field(AccessTools.Inner(typeof(CharacterCardUtility), "LeftRectSection"), "rect");

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
        if (DrawingPawn != pawn || measured == null || measured.Count == 0 || SectionRectField == null)
        {
            return;
        }

        float needed = FirstSectionGap + Margin;
        foreach (object section in measured)
        {
            needed += SectionRectField.GetValue(section) is Rect sectionRect ? sectionRect.height : 0f;
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

    public static bool CanMeasure => SectionRectField != null;

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
