using System;
using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace VanillaUIPlus;

/// <summary>
/// Typed access to Dialog_Search's protected members for one element type. Ready is false when any member is missing.
/// </summary>
public static class SearchDialogAccess<T>
    where T : class
{
    private const string Owner = "Dialog_Search";

    public static readonly AccessTools.FieldRef<Dialog_Search<T>, SortedList<string, T>>? Results = Field<SortedList<string, T>>("searchResults");
    public static readonly AccessTools.FieldRef<Dialog_Search<T>, T>? Highlighted = Field<T>("highlightedElement");
    public static readonly AccessTools.FieldRef<Dialog_Search<T>, Vector2>? ScrollPos = Field<Vector2>("scrollPos");
    public static readonly AccessTools.FieldRef<Dialog_Search<T>, bool>? TriedToFocus = Field<bool>("triedToFocus");
    public static readonly AccessTools.FieldRef<Dialog_Search<T>, int>? OpenFrames = Field<int>("openFrames");
    public static readonly AccessTools.FieldRef<Dialog_Search<T>, List<T>>? AllElements = Field<List<T>>("allElements");
    public static readonly AccessTools.FieldRef<Dialog_Search<T>, int>? SearchIndex = Field<int>("searchIndex");

    public static readonly Action<Dialog_Search<T>, T, Rect>? DoIcon = Method<Action<Dialog_Search<T>, T, Rect>>("DoIcon");
    public static readonly Action<Dialog_Search<T>, T, Rect>? DoLabel = Method<Action<Dialog_Search<T>, T, Rect>>("DoLabel");
    public static readonly Action<Dialog_Search<T>, T, Rect>? DoExtraIcon = Method<Action<Dialog_Search<T>, T, Rect>>("DoExtraIcon");
    public static readonly Action<Dialog_Search<T>, T>? Clicked = Method<Action<Dialog_Search<T>, T>>("ClikedOnElement");
    public static readonly Action<Dialog_Search<T>, T>? TryAddElement = Method<Action<Dialog_Search<T>, T>>("TryAddElement");
    public static readonly Func<Dialog_Search<T>, T, bool>? ShouldSkip = Method<Func<Dialog_Search<T>, T, bool>>("ShouldSkipElement");
    public static readonly Func<Dialog_Search<T>, TaggedString>? SearchLabel = Getter<TaggedString>("SearchLabel");
    public static readonly Func<Dialog_Search<T>, bool>? Searching = Getter<bool>("Searching");

    public static bool Ready => Results != null && Highlighted != null && ScrollPos != null && TriedToFocus != null
        && OpenFrames != null && AllElements != null && SearchIndex != null && DoIcon != null && DoLabel != null
        && DoExtraIcon != null && Clicked != null && TryAddElement != null && ShouldSkip != null && SearchLabel != null && Searching != null;

    private static AccessTools.FieldRef<Dialog_Search<T>, F>? Field<F>(string name)
    {
        return ReflectionGuard.FieldRef<Dialog_Search<T>, F>(Owner, name, AccessTools.Field(typeof(Dialog_Search<T>), name));
    }

    private static D? Method<D>(string name)
        where D : Delegate
    {
        return ReflectionGuard.Delegate<D>(Owner, name, AccessTools.Method(typeof(Dialog_Search<T>), name));
    }

    private static Func<Dialog_Search<T>, R>? Getter<R>(string name)
    {
        return ReflectionGuard.Delegate<Func<Dialog_Search<T>, R>>(Owner, name, AccessTools.PropertyGetter(typeof(Dialog_Search<T>), name));
    }
}
