using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace VanillaUIPlus;

/// <summary>
/// The list view of the readouts: rows of icon, label and value in the base game's list style, with related stats sharing a row.
/// </summary>
public static partial class PawnReadoutDrawer
{
    private const float ListCellGap = 8f;

    // Space before a row's label: the icon and its margins in list view, the card view's icon space otherwise.
    private static float RowIconSpace => ReadoutFont.ListView ? ReadoutFont.IconSize + 8f : IconSpace;

    // A row's icon at its left edge, centered on the row.
    private static Rect RowIconRect(Rect row)
    {
        if (!ReadoutFont.ListView)
        {
            return new Rect(row.x + 4f, row.y + 3f, 16f, 16f);
        }

        float size = ReadoutFont.IconSize;
        return new Rect(row.x + 4f, row.y + (row.height - size) / 2f, size, size);
    }

    private static void DrawIcon(Rect rect, int icon, Texture2D? texture, ThingDef? thing, Thing? instance)
    {
        if (texture != null)
        {
            GUI.DrawTexture(rect, texture, ScaleMode.ScaleToFit);
        }
        else if (instance != null)
        {
            Widgets.ThingIcon(rect, instance);
            GUI.color = Color.white;
        }
        else if (thing != null)
        {
            Widgets.ThingIcon(rect, thing);
        }
        else if (icon >= 0)
        {
            DrawStatIcon(rect, icon);
        }
    }

    // One stat in a shared row: the value right-aligned in a fixed-width column, with its icon just before that column so icons line up between rows.
    private static void ListCell(Rect cell, int icon, Texture2D? texture, ThingDef? thing, string value, Color valueColor)
    {
        float size = ReadoutFont.IconSize;
        float valueWidth = Mathf.Min(Mathf.Max(SharedValueWidth, ReadoutFont.Width(value) + 2f), cell.width - size - 4f);
        Rect valueRect = new Rect(cell.xMax - valueWidth, cell.y, valueWidth, cell.height);
        DrawIcon(new Rect(valueRect.x - 4f - size, cell.y + (cell.height - size) / 2f, size, size), icon, texture, thing, null);
        ReadoutFont.Label(valueRect, value, valueColor, TextAnchor.MiddleRight);
    }

    // Width of a shared row's value column: room for the widest usual value.
    private static float SharedValueWidth => Mathf.Max(ReadoutFont.Width("100%"), ReadoutFont.Width("10.00")) + 2f;

    // The grid shared rows use: three equal cells from where the other rows' bars start to the row's right edge, where their values end.
    private static Rect SharedCell(Rect row, int index)
    {
        float start = Mathf.Min(StatLabelWidth, row.width * 0.45f);
        Rect cells = new Rect(row.x + start, row.y, row.width - start - 4f, row.height);
        float width = (cells.width - ListCellGap * 2f) / 3f;
        return new Rect(cells.x + (width + ListCellGap) * index, cells.y, width, cells.height);
    }

    // A shared row's icon and label, placed like the other rows' icons and labels.
    private static void SharedRowLabel(Rect row, Texture2D icon, string label)
    {
        GUI.color = SharedRowIconColor;
        GUI.DrawTexture(RowIconRect(row), icon, ScaleMode.ScaleToFit);
        GUI.color = Color.white;
        ReadoutFont.Label(new Rect(row.x + RowIconSpace, row.y, Mathf.Min(StatLabelWidth, row.width * 0.45f) - RowIconSpace, row.height), label, LabelColor);
    }

    private static readonly Color SharedRowIconColor = new Color(0.8f, 0.8f, 0.8f);
    private static readonly Texture2D ArmorRowTex = ContentFinder<Texture2D>.Get("UI/Icons/MainButtons/shield");
    private static readonly Texture2D StatsRowTex = ContentFinder<Texture2D>.Get("UI/Icons/MainButtons/clipboard-pulse");

    // Armor label, then sharp, blunt and heat with their icons on one row.
    private static void ArmorListRow(Rect area, ref float y, PawnReadout.Snapshot snapshot)
    {
        Rect row = NextStatRow(area, ref y);
        SharedRowLabel(row, ArmorRowTex, TextCache.Key("VUIP.PawnPaneArmor"));
        ArmorCell(SharedCell(row, 0), 0, snapshot.ArmorSharpText, snapshot, 0x5C1A21);
        ArmorCell(SharedCell(row, 1), 1, snapshot.ArmorBluntText, snapshot, 0x5C1A22);
        ArmorCell(SharedCell(row, 2), 2, snapshot.ArmorHeatText, snapshot, 0x5C1A23);
    }

    private static void ArmorCell(Rect cell, int icon, string value, PawnReadout.Snapshot snapshot, int tipId)
    {
        ListCell(cell, icon, null, null, value, Color.white);
        Tip(cell, snapshot, ArmorTip, tipId);
    }

    // Movement, work speed and damage per second sharing one row, in the armor row's columns.
    private static void StatListRow(Rect area, ref float y, PawnReadout.Snapshot snapshot)
    {
        UiPlusSettings settings = UiPlusMod.Settings;
        Rect row = NextStatRow(area, ref y);
        SharedRowLabel(row, StatsRowTex, TextCache.Key("VUIP.PawnPaneStats"));
        int slot = 0;
        if (settings.pawnPaneShowSpeed)
        {
            Rect move = SharedCell(row, slot++);
            ListCell(move, 4, null, null, snapshot.MoveText, Color.white);
            Tip(move, snapshot, s => StatTip(s.Pawn, StatDefOf.MoveSpeed, s.MoveSpeed), 0x5C1A03);
            Rect work = SharedCell(row, slot++);
            ListCell(work, 5, null, null, snapshot.WorkText, Color.white);
            Tip(work, snapshot, s => StatTip(s.Pawn, StatDefOf.WorkSpeedGlobal, s.WorkSpeed), 0x5C1A09);
        }

        if (settings.pawnPaneShowDps)
        {
            Rect dps = SharedCell(row, slot);
            ListCell(dps, 6, null, null, snapshot.DpsText, snapshot.Ranged && snapshot.Dps < 0f ? LabelColor : Color.white);
            Tip(dps, snapshot, CombatTip, 0x5C1A04);
        }
    }

    // One full-width row: icon, label, and the value on the right; the value gives way to the label only past half the row.
    private static void ListRow(Rect row, int icon, Texture2D? texture, ThingDef? thing, Thing? instance, string caption, string value, Color valueColor)
    {
        bool hasIcon = icon >= 0 || texture != null || thing != null || instance != null;
        if (hasIcon)
        {
            DrawIcon(RowIconRect(row), icon, texture, thing, instance);
        }

        // Labels line up whether or not the row has an icon.
        float x = row.x + RowIconSpace;
        float available = row.xMax - 4f - x;
        float captionWidth = Mathf.Min(ReadoutFont.Width(caption) + 4f, available);
        float valueWidth = Mathf.Min(ReadoutFont.Width(value) + 2f, Mathf.Max(available - captionWidth - ListCellGap, available / 2f));
        ReadoutFont.Label(new Rect(row.xMax - 4f - valueWidth, row.y, valueWidth, row.height), value, valueColor, TextAnchor.MiddleRight);
        ReadoutFont.Label(new Rect(x, row.y, Mathf.Min(captionWidth, available - valueWidth - ListCellGap), row.height), caption, LabelColor);
    }

    internal static float CardListHeight(List<ReadoutCard> cards)
    {
        return cards.Count * PawnReadout.RowHeight;
    }

    // One row per card from y, with their tooltips and clicks; returns the y under them.
    private static float DrawCardList(Rect rect, float y, List<ReadoutCard> cards, int tipIdBase)
    {
        for (int i = 0; i < cards.Count; i++)
        {
            ReadoutCard card = cards[i];
            Rect row = NextStatRow(rect, ref y);
            ListRow(row, card.Icon, card.IconTexture, card.IconThing, card.IconInstance, card.Caption, card.Value, card.ValueColor);
            if (Mouse.IsOver(row))
            {
                TooltipHandler.TipRegion(row, new TipSignal(card.TipText, tipIdBase + i));
            }

            if (card.Click != null && Widgets.ButtonInvisible(row))
            {
                card.Click();
            }
        }

        return y;
    }

    // Creature rows: armor on one row, then a row per other card, with movement and damage sharing the last.
    private static int CreatureListRows(CreatureView view)
    {
        int rows = 0;
        bool armor = false;
        bool stats = false;
        for (int i = 0; i < view.Cards.Count; i++)
        {
            switch (view.Cards[i].Line)
            {
                case CreatureLine.Armor:
                    armor = true;
                    break;
                case CreatureLine.Stats:
                    stats = true;
                    break;
                case CreatureLine.Text:
                    rows++;
                    break;
                default:
                    rows++;
                    break;
            }
        }

        return rows + (armor ? 1 : 0) + (stats ? 1 : 0);
    }

    private static float DrawCreatureList(CreatureView view, Rect rect, float y)
    {
        if (view.Cards.Count > 0 && view.Cards[0].Line == CreatureLine.Armor)
        {
            ArmorListRow(rect, ref y, view.Snapshot);
        }

        int statCount = 0;
        for (int i = 0; i < view.Cards.Count; i++)
        {
            CreatureCard card = view.Cards[i];
            if (card.Line == CreatureLine.Stats)
            {
                statCount++;
            }
            else if (card.Line == CreatureLine.Own)
            {
                Rect row = NextStatRow(rect, ref y);
                ListRow(row, card.Icon, card.IconTexture, card.IconThing, null, card.ListCaption ?? card.Caption, card.Value, card.ValueColor);
                Tip(row, view.Snapshot, card.Tip, card.TipId);
            }
        }

        if (statCount > 0)
        {
            Rect statRow = NextStatRow(rect, ref y);
            SharedRowLabel(statRow, StatsRowTex, TextCache.Key("VUIP.PawnPaneStats"));
            int slot = 0;
            for (int i = 0; i < view.Cards.Count && slot < 3; i++)
            {
                CreatureCard card = view.Cards[i];
                if (card.Line != CreatureLine.Stats)
                {
                    continue;
                }

                Rect cell = SharedCell(statRow, slot++);
                ListCell(cell, card.Icon, card.IconTexture, card.IconThing, card.Value, card.ValueColor);
                Tip(cell, view.Snapshot, card.Tip, card.TipId);
            }
        }

        // Rows from the inspect text, each with its own tooltip id after the cards' ids.
        for (int i = 0; i < view.Cards.Count; i++)
        {
            CreatureCard card = view.Cards[i];
            if (card.Line == CreatureLine.Text)
            {
                Rect row = NextStatRow(rect, ref y);
                ListRow(row, -1, null, null, null, card.Caption, card.Value, card.ValueColor);
                Tip(row, view.Snapshot, card.Tip, 0x5C1A80 + i);
            }
        }

        return y;
    }

    private enum CreatureLine
    {
        Own,
        Armor,
        Stats,
        // A "Label: value" line from the inspect text, drawn after the stats row.
        Text
    }
}
