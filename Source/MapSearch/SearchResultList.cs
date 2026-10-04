using System;
using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace VanillaUIPlus;

public sealed class SearchResultGroup<T>
    where T : class
{
    public readonly object Key;
    public readonly string Label;
    public readonly List<T> Items = new List<T>();

    public SearchResultGroup(object key, string label)
    {
        Key = key;
        Label = label;
    }
}

/// <summary>
/// One search dialog's results grouped by kind and flattened into rows. Groups of one show just their item.
/// </summary>
public sealed class SearchResultList<T>
    where T : class
{
    public const float RowHeight = 26f;
    private const float IndentWidth = 26f;
    private const int RebuildIntervalWhileSearching = 10;
    private const int RefreshInterval = 60;

    // With this many results or fewer, every group is shown open.
    private const int ShowAllLimit = 8;

    private readonly Func<T, object?> keyOf;
    private readonly Func<object, string> groupLabel;
    private readonly Func<T, string?>? labelOverride;
    private readonly Func<bool> grouped;
    private readonly SearchResultGroup<T> flatGroup = new SearchResultGroup<T>(new object(), "");
    private bool flat;
    private readonly Dictionary<object, SearchResultGroup<T>> groups = new Dictionary<object, SearchResultGroup<T>>();
    private readonly List<SearchResultGroup<T>> ordered = new List<SearchResultGroup<T>>();
    private readonly HashSet<object> openKeys = new HashSet<object>();
    private readonly List<Row> rows = new List<Row>();
    private int builtCount = -1;
    private int builtFrame = -1;
    private int itemCount;
    private bool rowsDirty;

    public SearchResultGroup<T>? HoveredGroup { get; private set; }

    public int RowCount => rows.Count;

    private readonly struct Row
    {
        public readonly SearchResultGroup<T> Group;
        public readonly T? Item;
        public readonly bool Indented;

        public Row(SearchResultGroup<T> group, T? item, bool indented)
        {
            Group = group;
            Item = item;
            Indented = indented;
        }
    }

    public SearchResultList(Func<T, object?> keyOf, Func<object, string> groupLabel, Func<bool> grouped,
        Func<T, string?>? labelOverride = null)
    {
        this.grouped = grouped;
        this.keyOf = keyOf;
        this.groupLabel = groupLabel;
        this.labelOverride = labelOverride;
    }

    /// <summary>Regroups when the results changed; returns true when the row count changed.</summary>
    public bool Refresh(Dialog_Search<T> dialog, SortedList<string, T> results, bool searching)
    {
        int frame = Time.frameCount;
        bool due = results.Count != builtCount
            ? !searching || frame - builtFrame >= RebuildIntervalWhileSearching
            : frame - builtFrame >= RefreshInterval;
        if (!due && !rowsDirty)
        {
            return false;
        }

        int oldRows = rows.Count;
        if (due)
        {
            Regroup(dialog, results);
            builtCount = results.Count;
            builtFrame = frame;
        }

        RebuildRows();
        return rows.Count != oldRows;
    }

    private void Regroup(Dialog_Search<T> dialog, SortedList<string, T> results)
    {
        foreach (SearchResultGroup<T> group in groups.Values)
        {
            group.Items.Clear();
        }

        flatGroup.Items.Clear();
        flat = !grouped();
        itemCount = 0;
        IList<T> values = results.Values;
        for (int i = 0; i < values.Count; i++)
        {
            T item = values[i];
            if (item == null || SearchDialogAccess<T>.ShouldSkip!(dialog, item))
            {
                continue;
            }

            itemCount++;
            if (flat)
            {
                flatGroup.Items.Add(item);
                continue;
            }

            if (keyOf(item) is not { } key)
            {
                continue;
            }

            if (!groups.TryGetValue(key, out SearchResultGroup<T>? group))
            {
                group = new SearchResultGroup<T>(key, groupLabel(key));
                groups[key] = group;
            }

            group.Items.Add(item);
        }

        ordered.Clear();
        foreach (SearchResultGroup<T> group in groups.Values)
        {
            if (group.Items.Count > 0)
            {
                ordered.Add(group);
            }
        }

        ordered.Sort((a, b) => string.Compare(a.Label, b.Label, StringComparison.CurrentCultureIgnoreCase));
    }

    private void RebuildRows()
    {
        rowsDirty = false;
        rows.Clear();
        if (flat)
        {
            foreach (T item in flatGroup.Items)
            {
                rows.Add(new Row(flatGroup, item, indented: false));
            }

            return;
        }

        bool showAll = itemCount <= ShowAllLimit;
        foreach (SearchResultGroup<T> group in ordered)
        {
            if (group.Items.Count == 1)
            {
                rows.Add(new Row(group, group.Items[0], indented: false));
                continue;
            }

            rows.Add(new Row(group, null, indented: false));
            if (showAll || openKeys.Contains(group.Key))
            {
                foreach (T item in group.Items)
                {
                    rows.Add(new Row(group, item, indented: true));
                }
            }
        }
    }

    private bool IsOpen(SearchResultGroup<T> group)
    {
        return itemCount <= ShowAllLimit || openKeys.Contains(group.Key);
    }

    private void Toggle(SearchResultGroup<T> group)
    {
        if (!openKeys.Remove(group.Key))
        {
            openKeys.Add(group.Key);
        }

        rowsDirty = true;
    }

    public void Draw(Dialog_Search<T> dialog, Rect outRect, ref Vector2 scrollPos)
    {
        HoveredGroup = null;
        if (rows.Count == 0)
        {
            return;
        }

        float height = rows.Count * RowHeight;
        bool scrollbar = height > outRect.height;
        Rect viewRect = new Rect(0f, 0f, outRect.width - (scrollbar ? 16f : 0f), height);
        Widgets.BeginScrollView(outRect, ref scrollPos, viewRect);
        int first = Mathf.Max(0, Mathf.FloorToInt(scrollPos.y / RowHeight));
        int last = Mathf.Min(rows.Count - 1, Mathf.CeilToInt((scrollPos.y + outRect.height) / RowHeight));
        SearchResultGroup<T>? toggled = null;
        for (int i = first; i <= last; i++)
        {
            Rect rowRect = new Rect(0f, i * RowHeight, viewRect.width, RowHeight);
            if (i % 2 == 1)
            {
                Widgets.DrawLightHighlight(rowRect);
            }

            Row row = rows[i];
            if (row.Item == null)
            {
                if (DrawHeader(dialog, rowRect, row.Group))
                {
                    toggled = row.Group;
                }
            }
            else
            {
                DrawItem(dialog, rowRect, row.Item, row.Indented);
            }
        }

        Widgets.EndScrollView();
        if (toggled != null)
        {
            Toggle(toggled);
        }
    }

    private bool DrawHeader(Dialog_Search<T> dialog, Rect rowRect, SearchResultGroup<T> group)
    {
        Rect toggleRect = new Rect(rowRect.x, rowRect.y, IndentWidth, RowHeight);
        GUI.DrawTexture(toggleRect.ContractedBy(6f), IsOpen(group) ? TexButton.Collapse : TexButton.Reveal);
        Rect iconRect = new Rect(toggleRect.xMax, rowRect.y, RowHeight, RowHeight);
        SearchDialogAccess<T>.DoIcon!(dialog, group.Items[0], iconRect);
        Rect labelRect = new Rect(iconRect.xMax + 4f, rowRect.y, rowRect.xMax - iconRect.xMax - 8f, RowHeight);
        Text.Anchor = TextAnchor.MiddleLeft;
        Widgets.Label(labelRect, (group.Label + " (" + group.Items.Count + ")").Truncate(labelRect.width));
        Text.Anchor = TextAnchor.UpperLeft;

        if (Mouse.IsOver(rowRect))
        {
            Widgets.DrawHighlight(rowRect);
            HoveredGroup = group;
        }

        return Widgets.ButtonInvisible(rowRect);
    }

    private void DrawItem(Dialog_Search<T> dialog, Rect rowRect, T item, bool indented)
    {
        Rect iconRect = new Rect(rowRect.x + (indented ? IndentWidth : 0f), rowRect.y, RowHeight, RowHeight);
        SearchDialogAccess<T>.DoIcon!(dialog, item, iconRect);
        Rect extraRect = new Rect(rowRect.xMax - RowHeight, rowRect.y, RowHeight, RowHeight);
        SearchDialogAccess<T>.DoExtraIcon!(dialog, item, extraRect);
        Rect labelRect = new Rect(iconRect.xMax + 4f, rowRect.y, extraRect.xMin - iconRect.xMax - 8f, RowHeight);
        if (labelOverride?.Invoke(item) is { } label)
        {
            Widgets.Label(labelRect, label);
        }
        else
        {
            SearchDialogAccess<T>.DoLabel!(dialog, item, labelRect);
        }

        if (Mouse.IsOver(rowRect))
        {
            Widgets.DrawHighlight(rowRect);
            SearchDialogAccess<T>.Highlighted!(dialog) = item;
        }

        if (Widgets.ButtonInvisible(rowRect))
        {
            SearchDialogAccess<T>.Clicked!(dialog, item);
        }
    }
}
