using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace VanillaUIPlus;

/// <summary>
/// Groups the Animals tab by species under collapsible header rows; collapsed species are left out of the table's pawn list.
/// </summary>
public static class AnimalGroups
{
    public const int HeaderHeight = 28;

    private static readonly Color BorderColor = new Color(1f, 1f, 1f, 0.2f);

    private static readonly ConditionalWeakTable<PawnTable, List<AnimalGroup>> tableGroups = new ConditionalWeakTable<PawnTable, List<AnimalGroup>>();

    // Kept for the session, so a species stays collapsed across tab openings.
    private static readonly HashSet<ThingDef> collapsed = new HashSet<ThingDef>();

    public sealed class AnimalGroup
    {
        public ThingDef Race = null!;
        public List<Pawn> Pawns = new List<Pawn>();
        public int Start;
        public int End;
    }

    public static bool Applies(PawnTable table)
    {
        return UiPlusMod.Settings.groupAnimalsBySpecies && table is PawnTable_Animals;
    }

    public static List<AnimalGroup>? Get(PawnTable table)
    {
        return tableGroups.TryGetValue(table, out List<AnimalGroup>? groups) ? groups : null;
    }

    public static void Clear(PawnTable table)
    {
        tableGroups.Remove(table);
    }

    // Orders species as vanilla does (petness, body size, label), keeps each species' sorted order, and drops collapsed species' rows.
    public static List<Pawn> Group(PawnTable table, IEnumerable<Pawn> sorted)
    {
        List<AnimalGroup> groups = new List<AnimalGroup>();
        List<Pawn> visible = new List<Pawn>();
        IEnumerable<IGrouping<ThingDef, Pawn>> bySpecies = sorted.GroupBy(p => p.def)
            .OrderBy(g => g.Key.race?.petness ?? 0f)
            .ThenBy(g => g.Key.race?.baseBodySize ?? 0f)
            .ThenBy(g => g.Key.label);
        foreach (IGrouping<ThingDef, Pawn> species in bySpecies)
        {
            AnimalGroup group = new AnimalGroup { Race = species.Key, Start = visible.Count };
            foreach (Pawn pawn in species)
            {
                group.Pawns.Add(pawn);
                if (!collapsed.Contains(species.Key))
                {
                    visible.Add(pawn);
                }
            }

            group.End = visible.Count;
            groups.Add(group);
        }

        tableGroups.Remove(table);
        tableGroups.Add(table, groups);
        return visible;
    }

    public static void Draw(PawnTable table, List<AnimalGroup> groups, Vector2 position, ref Vector2 scrollPosition, Vector2 size,
        List<float> columnWidths, List<float> rowHeights, List<Pawn> pawns, List<LookTargets> lookTargets, float headerHeight, float heightNoScrollbar)
    {
        float availableWidth = size.x - 16f;
        List<PawnColumnDef> columns = table.Columns;
        int x = 0;
        for (int i = 0; i < columns.Count; i++)
        {
            int width = ColumnWidth(i, columns.Count, columnWidths, availableWidth, x);
            columns[i].Worker.DoHeader(new Rect((int)position.x + x, (int)position.y, width, (int)headerHeight), table);
            x += width;
        }

        Rect outRect = new Rect((int)position.x, (int)position.y + (int)headerHeight, (int)size.x, (int)size.y - (int)headerHeight);
        Rect viewRect = new Rect(0f, 0f, outRect.width - 16f, (int)heightNoScrollbar - (int)headerHeight);
        Widgets.BeginScrollView(outRect, ref scrollPosition, viewRect);
        ThingDef? toggled = null;
        int y = 0;
        foreach (AnimalGroup group in groups)
        {
            if (DrawHeader(new Rect(0f, y, viewRect.width, HeaderHeight), group, table, columns, columnWidths, availableWidth))
            {
                toggled = group.Race;
            }

            y += HeaderHeight;
            y = DrawRows(table, columns, group, y, scrollPosition, outRect.height, viewRect.width, availableWidth, columnWidths, rowHeights, pawns, lookTargets);
        }

        Widgets.EndScrollView();
        if (toggled != null)
        {
            Toggle(toggled, table);
        }
    }

    // Matches the table's integer column widths, with the last column taking the remainder.
    private static int ColumnWidth(int index, int count, List<float> columnWidths, float availableWidth, int usedWidth)
    {
        return index == count - 1 ? (int)(availableWidth - usedWidth) : (int)columnWidths[index];
    }

    // The title runs up to the first column with a group control; those columns get cells that apply to the whole species.
    private static bool DrawHeader(Rect rect, AnimalGroup group, PawnTable table, List<PawnColumnDef> columns, List<float> columnWidths, float availableWidth)
    {
        bool isCollapsed = collapsed.Contains(group.Race);
        GUI.color = BorderColor;
        Widgets.DrawLineHorizontal(rect.x, rect.y, rect.width);
        GUI.color = Color.white;
        Widgets.DrawLightHighlight(rect);
        if (Mouse.IsOver(rect))
        {
            Widgets.DrawHighlight(rect);
        }

        float titleWidth = -1f;
        int x = 0;
        for (int i = 0; i < columns.Count; i++)
        {
            int width = ColumnWidth(i, columns.Count, columnWidths, availableWidth, x);
            if (AnimalGroupControls.HasControl(columns[i]))
            {
                if (titleWidth < 0f)
                {
                    titleWidth = x;
                }

                AnimalGroupControls.Draw(new Rect(rect.x + x, rect.y, width, rect.height), columns[i], group.Pawns, table);
            }

            x += width;
        }

        Rect titleRect = new Rect(rect.x, rect.y, titleWidth < 0f ? rect.width : titleWidth, rect.height);
        Rect arrowRect = new Rect(titleRect.x + 6f, titleRect.y + (titleRect.height - 18f) / 2f, 18f, 18f);
        GUI.DrawTexture(arrowRect, isCollapsed ? TexButton.Reveal : TexButton.Collapse);
        Rect iconRect = new Rect(arrowRect.xMax + 6f, titleRect.y + 2f, titleRect.height - 4f, titleRect.height - 4f);
        Widgets.DefIcon(iconRect, group.Race);
        Rect labelRect = new Rect(iconRect.xMax + 6f, titleRect.y, titleRect.xMax - iconRect.xMax - 6f, titleRect.height);
        Text.Anchor = TextAnchor.MiddleLeft;
        Text.WordWrap = false;
        Widgets.Label(labelRect, "VUIP.AnimalGroupHeader".Translate(group.Race.LabelCap, group.Pawns.Count));
        Text.WordWrap = true;
        Text.Anchor = TextAnchor.UpperLeft;
        TooltipHandler.TipRegion(titleRect, isCollapsed ? "VUIP.AnimalGroupExpandTip".Translate() : "VUIP.AnimalGroupCollapseTip".Translate());
        return Widgets.ButtonInvisible(titleRect);
    }

    // Draws one species' rows as vanilla PawnTableOnGUI does, merging groupable cells only within the species.
    private static int DrawRows(PawnTable table, List<PawnColumnDef> columns, AnimalGroup group, int top, Vector2 scrollPosition, float visibleHeight,
        float rowWidth, float availableWidth, List<float> columnWidths, List<float> rowHeights, List<Pawn> pawns, List<LookTargets> lookTargets)
    {
        int x = 0;
        for (int i = 0; i < columns.Count; i++)
        {
            PawnColumnDef column = columns[i];
            int width = ColumnWidth(i, columns.Count, columnWidths, availableWidth, x);
            int y = top;
            for (int k = group.Start; k < group.End; k++)
            {
                GUI.color = BorderColor;
                Widgets.DrawLineHorizontal(x, y, width);
                GUI.color = Color.white;
                Rect cell = new Rect(x, y, width, (int)rowHeights[k]);
                Pawn pawn = pawns[k];
                bool merged = false;
                if (column.groupable)
                {
                    int last = k;
                    for (int l = k + 1; l < group.End && column.Worker.CanGroupWith(pawns[k], pawns[l]); l++)
                    {
                        cell.yMax += (int)rowHeights[l];
                        last = l;
                        merged = true;
                    }

                    k = last;
                }

                if (!(y - scrollPosition.y + (int)rowHeights[k] < 0f) && !(y - scrollPosition.y > visibleHeight))
                {
                    column.Worker.DoCell(cell, pawn, table);
                    if (merged)
                    {
                        GUI.color = BorderColor;
                        Widgets.DrawLineVertical(cell.xMin, cell.yMin, cell.height);
                        Widgets.DrawLineVertical(cell.xMax, cell.yMin, cell.height);
                        GUI.color = Color.white;
                    }
                }

                GUI.color = Color.white;
                y += (int)cell.height;
            }

            x += width;
        }

        int rowY = top;
        for (int k = group.Start; k < group.End; k++)
        {
            Rect row = new Rect(0f, rowY, rowWidth, (int)rowHeights[k]);
            if (Find.Selector.IsSelected(pawns[k]))
            {
                Widgets.DrawHighlight(row, 0.6f);
            }

            if (Mouse.IsOver(row))
            {
                Widgets.DrawHighlight(row);
                lookTargets[k].Highlight(arrow: true, pawns[k].IsColonist);
            }

            if (pawns[k].Downed)
            {
                GUI.color = new Color(1f, 0f, 0f, 0.5f);
                Widgets.DrawLineHorizontal(0f, row.center.y, rowWidth);
                GUI.color = Color.white;
            }

            rowY += (int)rowHeights[k];
        }

        return rowY;
    }

    private static void Toggle(ThingDef race, PawnTable table)
    {
        if (collapsed.Remove(race))
        {
            SoundDefOf.TabOpen.PlayOneShotOnCamera();
        }
        else
        {
            collapsed.Add(race);
            SoundDefOf.TabClose.PlayOneShotOnCamera();
        }

        // Notify_PawnsChanged also resizes the tab to the new table height.
        table.SetDirty();
        Find.WindowStack.WindowOfType<MainTabWindow_Animals>()?.Notify_PawnsChanged();
    }
}
