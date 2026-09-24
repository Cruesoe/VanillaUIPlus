using System;
using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace VanillaUIPlus;

/// <summary>
/// Drag-to-reorder, map-wide counting, hiding and zero-count rows for the resource readout; only the readout changes, not item categories.
/// Keys are "C:defName" for categories and "T:defName" for things; unsaved rows keep vanilla order after saved ones.
/// </summary>
public static class ResourceReadoutTweaks
{
    public const int CategorizedOpenMask = 32;
    public const float SimpleRowHeight = 24f;

    private const string CategoryPrefix = "C:";
    private const string ThingPrefix = "T:";
    public const string TopLevelKey = "TOP";

    private static UiPlusSettings Settings => UiPlusMod.Settings;

    public static readonly Action<Listing_ResourceReadout, ThingDef, int>? DoThingDef =
        ReflectionGuard.Delegate<Action<Listing_ResourceReadout, ThingDef, int>>(
            nameof(Listing_ResourceReadout), "DoThingDef",
            AccessTools.Method(typeof(Listing_ResourceReadout), "DoThingDef"));

    // Bumped on any order, move or count-all change; every cache below rebuilds on next read.
    private static int version;
    private static int cacheVersion = -1;

    private static readonly Dictionary<string, int> SimpleRanks = new Dictionary<string, int>();
    private static readonly Dictionary<string, int> CategorizedRanks = new Dictionary<string, int>();
    private static readonly HashSet<ThingDef> CountAllDefs = new HashSet<ThingDef>();
    private static readonly HashSet<Def> HiddenDefs = new HashSet<Def>();
    private static readonly Dictionary<ThingCategoryDef, List<Def>> OrderedChildren = new Dictionary<ThingCategoryDef, List<Def>>();
    private static readonly List<Def> OrderedTopLevel = new List<Def>();
    private static List<ThingCategoryDef>? orderedTopLevelSource;
    private static readonly List<Def> OrderedSimple = new List<Def>();
    private static Dictionary<ThingDef, int>? orderedSimpleSource;
    private static int orderedSimpleSourceCount = -1;
    private static readonly Dictionary<ThingCategoryDef, bool> HasResourcesCache = new Dictionary<ThingCategoryDef, bool>();

    // Category totals for one counter, cleared whenever counts or settings change.
    private static readonly Dictionary<ThingCategoryDef, int> CategoryTotals = new Dictionary<ThingCategoryDef, int>();
    private static ResourceCounter? totalsCounter;

    // Rows moved out of their vanilla list, with their new parent (null for the top level), and the reverse lookup.
    private static readonly Dictionary<Def, ThingCategoryDef?> Moved = new Dictionary<Def, ThingCategoryDef?>();
    private static readonly Dictionary<ThingCategoryDef, List<Def>> MovedInto = new Dictionary<ThingCategoryDef, List<Def>>();
    private static readonly List<Def> MovedToTop = new List<Def>();

    // Map-wide totals for count-all things, refreshed after vanilla's counter updates.
    private static readonly Dictionary<ThingDef, int> MapCounts = new Dictionary<ThingDef, int>();
    private static Map? countedMap;
    private static bool mapCountsDirty = true;

    private static readonly AccessTools.FieldRef<ResourceCounter, Map>? CounterMap =
        ReflectionGuard.FieldRef<ResourceCounter, Map>(nameof(ResourceCounter), "map",
            AccessTools.Field(typeof(ResourceCounter), "map"));

    private sealed class DragGroup
    {
        public int id = -1;
        public bool simple;
        public ThingCategoryDef? parent;
        public readonly List<Def> rows = new List<Def>();
        public List<Def> all = new List<Def>();
        public Action<int, int>? onReorder;
    }

    // Keyed by parent and open mask, so Dubs Mint Menus' pinned copy stays out of the joined set.
    private static readonly Dictionary<(Def?, int), DragGroup> Groups = new Dictionary<(Def?, int), DragGroup>();
    private static readonly Stack<DragGroup> Parents = new Stack<DragGroup>();
    private static DragGroup? simpleGroup;

    // The categorized lists drawn this repaint, for the joined drag set.
    private static readonly Dictionary<int, DragGroup> JoinedById = new Dictionary<int, DragGroup>();
    private static readonly List<int> JoinedIds = new List<int>();
    private static readonly Action<int, int, int, int> OnMoveAcross = MoveAcross;

    // Set once the top-level loop is taken over; vanilla's own loop then draws nothing.
    public static bool DrawsTopLevel;
    private static readonly List<ThingCategoryDef> NoCategories = new List<ThingCategoryDef>();

    public static string KeyFor(Def def)
    {
        return (def is ThingCategoryDef ? CategoryPrefix : ThingPrefix) + def.defName;
    }

    public static bool HasCustomOrder =>
        Settings.resourceOrderSimple.Count > 0 || Settings.resourceOrderCategorized.Count > 0 || Settings.resourceParents.Count > 0;

    public static void ResetOrder()
    {
        Settings.resourceOrderSimple.Clear();
        ResetCategorized();
    }

    private static void ResetCategorized()
    {
        Settings.resourceOrderCategorized.Clear();
        Settings.resourceParents.Clear();
        NotifyChanged();
    }

    public static void ShowAllHidden()
    {
        Settings.resourceHidden.Clear();
        NotifyChanged();
    }

    public static void ClearCountAll()
    {
        Settings.resourceCountAll.Clear();
        NotifyChanged();
    }

    public static void NotifyChanged()
    {
        version++;
        mapCountsDirty = true;
        HasResourcesCache.Clear();
        CategoryTotals.Clear();
        UiPlusMod.Instance?.WriteSettings();
    }

    // Called after ResourceCounter.UpdateResourceCounts, the only place vanilla changes counts.
    public static void NotifyCountsUpdated()
    {
        mapCountsDirty = true;
        CategoryTotals.Clear();
        orderedSimpleSource = null;
    }

    // ---- Counting -----------------------------------------------------------------------

    public static int Count(ResourceCounter counter, ThingDef def)
    {
        EnsureCaches();
        if (CountAllDefs.Count == 0 || !CountAllDefs.Contains(def) || CounterMap == null)
        {
            return counter.GetCount(def);
        }

        Map map = CounterMap(counter);
        if (map == null)
        {
            return counter.GetCount(def);
        }

        EnsureMapCounts(map);
        return MapCounts.TryGetValue(def, out int count) ? count : 0;
    }

    public static int CountIn(ResourceCounter counter, ThingCategoryDef category)
    {
        EnsureCaches();
        bool skipHidden = !Settings.countHiddenInTotals && HiddenDefs.Count > 0;
        if (CountAllDefs.Count == 0 && Moved.Count == 0 && !skipHidden)
        {
            return counter.GetCountIn(category);
        }

        if (!ReferenceEquals(totalsCounter, counter))
        {
            totalsCounter = counter;
            CategoryTotals.Clear();
        }

        if (CategoryTotals.TryGetValue(category, out int cached))
        {
            return cached;
        }

        // Vanilla's GetCountIn walk, over the category as the readout shows it.
        int total = 0;
        foreach (Def child in Children(category))
        {
            if (skipHidden && HiddenDefs.Contains(child))
            {
                continue;
            }

            total += child is ThingCategoryDef inner ? CountIn(counter, inner) : Count(counter, (ThingDef)child);
        }

        CategoryTotals[category] = total;
        return total;
    }

    public static bool IsCountedAll(ThingDef def)
    {
        EnsureCaches();
        return CountAllDefs.Contains(def);
    }

    // True for a category ticked itself; its things answer through IsCountedAll.
    private static bool IsMarkedCountAll(Def def)
    {
        return def is ThingDef thing ? IsCountedAll(thing) : Settings.resourceCountAll.Contains(KeyFor(def));
    }

    private static void EnsureMapCounts(Map map)
    {
        if (!mapCountsDirty && countedMap == map)
        {
            return;
        }

        mapCountsDirty = false;
        countedMap = map;
        MapCounts.Clear();
        foreach (ThingDef def in CountAllDefs)
        {
            int total = 0;
            List<Thing> things = map.listerThings.ThingsOfDef(def);
            for (int i = 0; i < things.Count; i++)
            {
                Thing thing = things[i];
                // Installed furniture shares its def with the minified item but is not stock.
                if (thing.def.category != ThingCategory.Building && ShouldCount(thing, thing, map))
                {
                    total += thing.stackCount;
                }
            }

            MapCounts[def] = total;
        }

        List<Thing> minified = map.listerThings.ThingsInGroup(ThingRequestGroup.MinifiedThing);
        for (int i = 0; i < minified.Count; i++)
        {
            Thing inner = minified[i].GetInnerIfMinified();
            if (inner != minified[i] && CountAllDefs.Contains(inner.def) && ShouldCount(minified[i], inner, map))
            {
                MapCounts[inner.def] += inner.stackCount;
            }
        }

        // Things held inside storage buildings, which the listers above miss.
        List<SlotGroup> groups = map.haulDestinationManager.AllGroupsListForReading;
        for (int i = 0; i < groups.Count; i++)
        {
            foreach (Thing held in groups[i].HeldThings)
            {
                if (held.Spawned)
                {
                    continue;
                }

                Thing inner = held.GetInnerIfMinified();
                if (CountAllDefs.Contains(inner.def) && !inner.IsNotFresh())
                {
                    MapCounts[inner.def] += inner.stackCount;
                }
            }
        }
    }

    // Matches vanilla's rule for stockpiles: no rotten food and nothing under fog.
    private static bool ShouldCount(Thing outer, Thing inner, Map map)
    {
        return !inner.IsNotFresh() && !outer.Position.Fogged(map);
    }

    // ---- Visibility ---------------------------------------------------------------------

    // Hidden rows are left out of the readout but still count towards their category.
    public static bool ShowThing(int count, ThingDef def)
    {
        return !IsHidden(def) && (count != 0 || (Settings.showZeroResources && def.CountAsResource));
    }

    public static bool ShowCategory(int count, TreeNode_ThingCategory node)
    {
        return !IsHidden(node.catDef) && (count != 0 || (Settings.showZeroResources && HasResources(node.catDef)));
    }

    public static bool ShowSimple(int count, ThingDef def)
    {
        return !IsHidden(def) && (count > 0 || def.resourceReadoutAlwaysShow || (Settings.showZeroResources && def.PlayerAcquirable));
    }

    private static bool IsHidden(Def def)
    {
        EnsureCaches();
        return HiddenDefs.Count > 0 && HiddenDefs.Contains(def);
    }

    // Categories that could never hold a counted resource stay hidden even with zero counts shown.
    private static bool HasResources(ThingCategoryDef category)
    {
        if (HasResourcesCache.TryGetValue(category, out bool has))
        {
            return has;
        }

        HasResourcesCache[category] = false;
        foreach (Def child in Children(category))
        {
            if (child is ThingCategoryDef inner ? HasResources(inner) : IsAcquirableResource((ThingDef)child))
            {
                has = true;
                break;
            }
        }

        HasResourcesCache[category] = has;
        return has;
    }

    private static bool IsAcquirableResource(ThingDef def)
    {
        return def.CountAsResource && def.PlayerAcquirable;
    }

    // ---- Lists and order ----------------------------------------------------------------

    // Every read of vanilla's top-level list; empty once DrawTopLevel draws it.
    public static List<ThingCategoryDef> VanillaTopLevel(List<ThingCategoryDef> vanilla)
    {
        if (orderedTopLevelSource != vanilla)
        {
            orderedTopLevelSource = vanilla;
            OrderedTopLevel.Clear();
        }

        return DrawsTopLevel && DoThingDef != null ? NoCategories : vanilla;
    }

    public static List<Def> TopLevel()
    {
        EnsureCaches();
        if (OrderedTopLevel.Count == 0 && orderedTopLevelSource != null)
        {
            foreach (ThingCategoryDef category in orderedTopLevelSource)
            {
                if (!Moved.ContainsKey(category))
                {
                    OrderedTopLevel.Add(category);
                }
            }

            OrderedTopLevel.AddRange(MovedToTop);
            Sort(OrderedTopLevel, CategorizedRanks);
        }

        return OrderedTopLevel;
    }

    public static List<Def> Children(ThingCategoryDef category)
    {
        EnsureCaches();
        if (OrderedChildren.TryGetValue(category, out List<Def> children))
        {
            return children;
        }

        children = new List<Def>();
        foreach (ThingCategoryDef child in category.childCategories)
        {
            if (!child.resourceReadoutRoot && !Moved.ContainsKey(child))
            {
                children.Add(child);
            }
        }

        foreach (ThingDef thing in category.childThingDefs)
        {
            if (!Moved.ContainsKey(thing))
            {
                children.Add(thing);
            }
        }

        if (MovedInto.TryGetValue(category, out List<Def> added))
        {
            children.AddRange(added);
        }

        Sort(children, CategorizedRanks);
        OrderedChildren[category] = children;
        return children;
    }

    public static List<Def> Simple(Dictionary<ThingDef, int> counted)
    {
        EnsureCaches();
        if (!ReferenceEquals(orderedSimpleSource, counted) || orderedSimpleSourceCount != counted.Count)
        {
            orderedSimpleSource = counted;
            orderedSimpleSourceCount = counted.Count;
            OrderedSimple.Clear();
            foreach (ThingDef def in counted.Keys)
            {
                OrderedSimple.Add(def);
            }

            Sort(OrderedSimple, SimpleRanks);
        }

        return OrderedSimple;
    }

    private static void Sort<T>(List<T> items, Dictionary<string, int> ranks)
        where T : Def
    {
        if (ranks.Count == 0)
        {
            return;
        }

        List<(T def, int rank, int index)> keyed = new List<(T, int, int)>(items.Count);
        for (int i = 0; i < items.Count; i++)
        {
            int rank = ranks.TryGetValue(KeyFor(items[i]), out int r) ? r : int.MaxValue;
            keyed.Add((items[i], rank, i));
        }

        keyed.Sort((a, b) => a.rank != b.rank ? a.rank.CompareTo(b.rank) : a.index.CompareTo(b.index));
        for (int i = 0; i < keyed.Count; i++)
        {
            items[i] = keyed[i].def;
        }
    }

    private static void EnsureCaches()
    {
        if (cacheVersion == version)
        {
            return;
        }

        cacheVersion = version;
        FillRanks(SimpleRanks, Settings.resourceOrderSimple);
        FillRanks(CategorizedRanks, Settings.resourceOrderCategorized);
        OrderedChildren.Clear();
        OrderedTopLevel.Clear();
        orderedSimpleSource = null;
        HasResourcesCache.Clear();
        CategoryTotals.Clear();
        LoadMoves();

        HiddenDefs.Clear();
        foreach (string key in Settings.resourceHidden)
        {
            if (Resolve(key) is Def hidden)
            {
                HiddenDefs.Add(hidden);
            }
        }

        CountAllDefs.Clear();
        foreach (string key in Settings.resourceCountAll)
        {
            Def? def = Resolve(key);
            if (def is ThingDef thing && thing.CountAsResource)
            {
                CountAllDefs.Add(thing);
            }
            else if (def is ThingCategoryDef category)
            {
                AddDescendants(category, 0);
            }
        }
    }

    private static void FillRanks(Dictionary<string, int> ranks, List<string> saved)
    {
        ranks.Clear();
        for (int i = 0; i < saved.Count; i++)
        {
            ranks[saved[i]] = i;
        }
    }

    private static void AddDescendants(ThingCategoryDef category, int depth)
    {
        if (depth > MaxDepth)
        {
            return;
        }

        foreach (Def child in Children(category))
        {
            if (child is ThingCategoryDef inner)
            {
                AddDescendants(inner, depth + 1);
            }
            else if (child is ThingDef thing && thing.CountAsResource)
            {
                CountAllDefs.Add(thing);
            }
        }
    }

    private static Def? Resolve(string key)
    {
        if (key.StartsWith(ThingPrefix))
        {
            return DefDatabase<ThingDef>.GetNamedSilentFail(key.Substring(ThingPrefix.Length));
        }

        if (key.StartsWith(CategoryPrefix))
        {
            return DefDatabase<ThingCategoryDef>.GetNamedSilentFail(key.Substring(CategoryPrefix.Length));
        }

        return null;
    }

    // ---- Moves between lists ------------------------------------------------------------

    private const int MaxDepth = 64;

    private static void LoadMoves()
    {
        Moved.Clear();
        MovedInto.Clear();
        MovedToTop.Clear();
        foreach (KeyValuePair<string, string> entry in Settings.resourceParents)
        {
            Def? def = Resolve(entry.Key);
            if (def == null)
            {
                continue;
            }

            if (entry.Value == TopLevelKey)
            {
                Moved[def] = null;
            }
            else if (Resolve(entry.Value) is ThingCategoryDef parent)
            {
                Moved[def] = parent;
            }
        }

        // A saved move that would put a category inside itself is ignored.
        List<Def> looping = new List<Def>();
        foreach (Def def in Moved.Keys)
        {
            if (def is ThingCategoryDef category && IsWithin(ParentOf(category), category))
            {
                looping.Add(def);
            }
        }

        foreach (Def def in looping)
        {
            Moved.Remove(def);
        }

        foreach (KeyValuePair<Def, ThingCategoryDef?> entry in Moved)
        {
            if (entry.Value == null)
            {
                MovedToTop.Add(entry.Key);
                continue;
            }

            if (!MovedInto.TryGetValue(entry.Value, out List<Def> list))
            {
                list = new List<Def>();
                MovedInto[entry.Value] = list;
            }

            list.Add(entry.Key);
        }
    }

    // A row's saved parent if moved, else its first vanilla category; null is the top level.
    private static ThingCategoryDef? ParentOf(Def def)
    {
        if (Moved.TryGetValue(def, out ThingCategoryDef? moved))
        {
            return moved;
        }

        if (def is ThingCategoryDef category)
        {
            return category.resourceReadoutRoot ? null : category.parent;
        }

        List<ThingCategoryDef>? categories = (def as ThingDef)?.thingCategories;
        return categories != null && categories.Count > 0 ? categories[0] : null;
    }

    // True when start is the category or somewhere inside it.
    private static bool IsWithin(ThingCategoryDef? start, ThingCategoryDef category)
    {
        ThingCategoryDef? current = start;
        for (int depth = 0; current != null && depth < MaxDepth; depth++)
        {
            if (current == category)
            {
                return true;
            }

            current = ParentOf(current);
        }

        return false;
    }

    private static bool IsVanillaParent(Def def, ThingCategoryDef? parent)
    {
        if (def is ThingCategoryDef category)
        {
            return parent == (category.resourceReadoutRoot ? null : category.parent);
        }

        // Any move pins a thing listed under several categories to one place.
        List<ThingCategoryDef>? categories = (def as ThingDef)?.thingCategories;
        return parent != null && categories != null && categories.Count == 1 && categories[0] == parent;
    }

    private static void SetParent(Def def, ThingCategoryDef? parent)
    {
        string key = KeyFor(def);
        if (IsVanillaParent(def, parent))
        {
            Settings.resourceParents.Remove(key);
        }
        else
        {
            Settings.resourceParents[key] = parent == null ? TopLevelKey : KeyFor(parent);
        }
    }

    // ---- Dragging and the right-click menu ----------------------------------------------

    // Draws the categorized top level in place of vanilla's loop, joining every open list into one drag set.
    public static void DrawTopLevel(Listing_ResourceReadout listing)
    {
        if (!DrawsTopLevel || DoThingDef == null)
        {
            return;
        }

        bool repaint = Event.current.type == EventType.Repaint;
        if (repaint)
        {
            JoinedById.Clear();
            JoinedIds.Clear();
        }

        List<Def> top = TopLevel();
        Parents.Push(BeginGroup(null, CategorizedOpenMask, top, simple: false));
        try
        {
            DrawList(listing, top, 0, 0, CategorizedOpenMask);
        }
        finally
        {
            Parents.Clear();
        }

        if (repaint && Settings.dragToReorderResources && JoinedIds.Count > 1)
        {
            ReorderableWidget.NewMultiGroup(JoinedIds, OnMoveAcross);
        }
    }

    public static void DrawChildren(Listing_ResourceReadout listing, TreeNode_ThingCategory node, int indentLevel, int openMask)
    {
        List<Def> children = Children(node.catDef);
        Parents.Push(BeginGroup(node.catDef, openMask, children, simple: false));
        try
        {
            DrawList(listing, children, indentLevel, indentLevel + 1, openMask);
        }
        finally
        {
            if (Parents.Count > 0)
            {
                Parents.Pop();
            }
        }
    }

    private static void DrawList(Listing_ResourceReadout listing, List<Def> list, int categoryNest, int thingNest, int openMask)
    {
        foreach (Def child in list)
        {
            if (child is ThingCategoryDef category)
            {
                listing.DoCategory(category.treeNode, categoryNest, openMask);
            }
            else if (child is ThingDef thing && thing.PlayerAcquirable)
            {
                DoThingDef!(listing, thing, thingNest);
            }
        }
    }

    public static void EndTopLevel()
    {
        Parents.Clear();
    }

    public static void BeginSimple(List<Def> all)
    {
        simpleGroup = BeginGroup(null, 0, all, simple: true);
    }

    public static void EndSimple()
    {
        simpleGroup = null;
    }

    public static void CategoryRow(Listing_ResourceReadout listing, TreeNode_ThingCategory node, int nestLevel, int openMask)
    {
        // Rows outside our lists, such as Dubs Mint Menus' pinned section, are left alone.
        Row(Parents.Count > 0 ? Parents.Peek() : null, node.catDef, TreeRowRect(listing, nestLevel));
    }

    public static void ThingRow(Listing_ResourceReadout listing, ThingDef def, int nestLevel)
    {
        Row(Parents.Count > 0 ? Parents.Peek() : null, def, TreeRowRect(listing, nestLevel));
    }

    public static void SimpleRow(Rect rect, ThingDef def)
    {
        Row(simpleGroup, def, rect);
    }

    // The rect vanilla uses for the row, right of the open/close arrow.
    private static Rect TreeRowRect(Listing_ResourceReadout listing, int nestLevel)
    {
        Rect rect = new Rect(0f, listing.CurHeight, listing.ColumnWidth, listing.lineHeight);
        rect.xMin = nestLevel * listing.nestIndentWidth + 18f;
        return rect;
    }

    private static DragGroup BeginGroup(ThingCategoryDef? parent, int openMask, List<Def> all, bool simple)
    {
        if (!Groups.TryGetValue((parent, openMask), out DragGroup group))
        {
            group = new DragGroup { simple = simple, parent = parent };
            DragGroup captured = group;
            group.onReorder = (from, to) => Reorder(captured, from, to);
            Groups[(parent, openMask)] = group;
        }

        group.all = all;

        // Group ids exist only on repaint; other events reuse the last one, as vanilla does.
        if (Event.current.type == EventType.Repaint)
        {
            group.rows.Clear();
            // A zero-size area, or hovering anywhere in a list would drop at its end.
            group.id = Settings.dragToReorderResources
                ? ReorderableWidget.NewGroup(group.onReorder, ReorderableDirection.Vertical, Rect.zero)
                : -1;

            if (group.id >= 0 && !simple && openMask == CategorizedOpenMask)
            {
                JoinedById[group.id] = group;
                JoinedIds.Add(group.id);
            }
        }

        return group;
    }

    private static void Row(DragGroup? group, Def def, Rect rect)
    {
        Event current = Event.current;
        if (Mouse.IsOver(rect) && IsMarkedCountAll(def))
        {
            TooltipHandler.TipRegionByKey(rect, "VUIP.ResourceCountingAllTip");
        }

        if (group != null && group.id >= 0 && Settings.dragToReorderResources)
        {
            if (current.type == EventType.Repaint)
            {
                group.rows.Add(def);
            }

            ReorderableWidget.Reorderable(group.id, rect);

            // Otherwise the click also starts a box selection on the map underneath.
            if (current.type == EventType.MouseDown && current.button == 0 && Mouse.IsOver(rect))
            {
                current.Use();
            }
        }

        if (Settings.resourceRightClickMenu && current.type == EventType.MouseDown && current.button == 1 && Mouse.IsOver(rect))
        {
            OpenMenu(def, group?.simple ?? !Prefs.ResourceReadoutCategorized);
            current.Use();
        }
    }

    private static void Reorder(DragGroup group, int from, int to)
    {
        if (from < 0 || from >= group.rows.Count)
        {
            return;
        }

        Def dragged = group.rows[from];
        SaveOrder(group, Place(group, dragged, to));
        NotifyChanged();
    }

    private static void MoveAcross(int from, int fromId, int to, int toId)
    {
        if (!JoinedById.TryGetValue(fromId, out DragGroup source)
            || !JoinedById.TryGetValue(toId, out DragGroup target)
            || from < 0 || from >= source.rows.Count)
        {
            return;
        }

        Def dragged = source.rows[from];
        if (dragged is ThingCategoryDef category && IsWithin(target.parent, category))
        {
            Messages.Message("VUIP.ResourceMoveIntoItself".Translate(), MessageTypeDefOf.RejectInput, historical: false);
            return;
        }

        SetParent(dragged, target.parent);
        SaveOrder(target, Place(target, dragged, to));
        NotifyChanged();
    }

    // The list order with the dragged row placed at "to", which counts visible rows only.
    private static List<Def> Place(DragGroup group, Def dragged, int to)
    {
        List<Def> order = new List<Def>(group.all);
        order.Remove(dragged);

        int index = -1;
        if (to >= 0 && to < group.rows.Count)
        {
            index = order.IndexOf(group.rows[to]);
        }
        else if (group.rows.Count > 0)
        {
            Def last = group.rows[group.rows.Count - 1];
            index = last == dragged ? -1 : order.IndexOf(last) + 1;
        }

        if (index < 0)
        {
            index = order.Count;
        }

        order.Insert(index, dragged);
        return order;
    }

    private static void SaveOrder(DragGroup group, List<Def> order)
    {
        List<string> saved = group.simple ? Settings.resourceOrderSimple : Settings.resourceOrderCategorized;
        HashSet<string> keys = new HashSet<string>();
        foreach (Def def in order)
        {
            keys.Add(KeyFor(def));
        }

        // Ranks only compare siblings, so the list is rewritten as one block at the end.
        saved.RemoveAll(keys.Contains);
        foreach (Def def in order)
        {
            saved.Add(KeyFor(def));
        }
    }

    private static void OpenMenu(Def def, bool simple)
    {
        EnsureCaches();
        List<FloatMenuOption> options = new List<FloatMenuOption>();
        string key = KeyFor(def);
        bool own = Settings.resourceCountAll.Contains(key);
        bool inherited = !own && def is ThingDef thing && IsCountedAll(thing);

        string countLabel = "VUIP.ResourceCountAllOnMap".Translate();
        if (inherited)
        {
            countLabel += " (" + "VUIP.ResourceCountAllInherited".Translate() + ")";
        }

        options.Add(new FloatMenuOption(
            countLabel,
            inherited ? null : () => ToggleCountAll(key),
            own || inherited ? Widgets.CheckboxOnTex : Widgets.CheckboxOffTex,
            Color.white,
            mouseoverGuiAction: rect => TooltipHandler.TipRegionByKey(rect, "VUIP.ResourceCountAllOnMapTip"),
            iconJustification: HorizontalJustification.Right));

        options.Add(new FloatMenuOption(
            "VUIP.ShowZeroResources".Translate(),
            () =>
            {
                Settings.showZeroResources = !Settings.showZeroResources;
                NotifyChanged();
            },
            Settings.showZeroResources ? Widgets.CheckboxOnTex : Widgets.CheckboxOffTex,
            Color.white,
            mouseoverGuiAction: rect => TooltipHandler.TipRegionByKey(rect, "VUIP.ShowZeroResourcesTip"),
            iconJustification: HorizontalJustification.Right));

        if (!simple && Settings.resourceParents.ContainsKey(key))
        {
            options.Add(new FloatMenuOption("VUIP.ReturnResourceToGroup".Translate(), () =>
            {
                Settings.resourceParents.Remove(key);
                NotifyChanged();
            }));
        }

        options.Add(new FloatMenuOption("VUIP.HideResource".Translate(), () =>
        {
            if (!Settings.resourceHidden.Contains(key))
            {
                Settings.resourceHidden.Add(key);
            }

            NotifyChanged();
        }));

        if (HiddenDefs.Count > 0)
        {
            options.Add(new FloatMenuOption("VUIP.UnhideResources".Translate(HiddenDefs.Count), OpenUnhideMenu));
        }

        bool hasOrder = simple
            ? Settings.resourceOrderSimple.Count > 0
            : Settings.resourceOrderCategorized.Count > 0 || Settings.resourceParents.Count > 0;
        if (hasOrder)
        {
            options.Add(new FloatMenuOption("VUIP.ResetResourceOrder".Translate(), () =>
            {
                if (simple)
                {
                    Settings.resourceOrderSimple.Clear();
                    NotifyChanged();
                }
                else
                {
                    ResetCategorized();
                }
            }));
        }

        Find.WindowStack.Add(new FloatMenu(options, def.LabelCap));
    }

    private static void OpenUnhideMenu()
    {
        List<FloatMenuOption> options = new List<FloatMenuOption>();
        foreach (string key in Settings.resourceHidden)
        {
            if (Resolve(key) is not Def def)
            {
                continue;
            }

            string captured = key;
            options.Add(new FloatMenuOption(def.LabelCap, () =>
            {
                Settings.resourceHidden.Remove(captured);
                NotifyChanged();
            }));
        }

        options.SortBy(option => option.Label);
        options.Add(new FloatMenuOption("VUIP.ShowAllHiddenResources".Translate(), ShowAllHidden));
        Find.WindowStack.Add(new FloatMenu(options));
    }

    private static void ToggleCountAll(string key)
    {
        if (!Settings.resourceCountAll.Remove(key))
        {
            Settings.resourceCountAll.Add(key);
        }

        NotifyChanged();
    }
}
