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

    // One stat in a shared row: icon, the caption when it fits in full, and the value on the right.
    private static void ListCell(Rect cell, int icon, Texture2D? texture, ThingDef? thing, Thing? instance, string caption, string value, Color valueColor)
    {
        float size = ReadoutFont.IconSize;
        bool hasIcon = icon >= 0 || texture != null || thing != null || instance != null;
        float x = cell.x;
        if (hasIcon)
        {
            DrawIcon(new Rect(x, cell.y + (cell.height - size) / 2f, size, size), icon, texture, thing, instance);
            x += size + 4f;
        }

        float valueWidth = Mathf.Min(ReadoutFont.Width(value) + 2f, cell.xMax - x);
        ReadoutFont.Label(new Rect(cell.xMax - valueWidth, cell.y, valueWidth, cell.height), value, valueColor, TextAnchor.MiddleRight);
        float captionWidth = cell.xMax - valueWidth - 4f - x;
        if (caption.Length > 0 && ReadoutFont.Width(caption) <= captionWidth)
        {
            ReadoutFont.Label(new Rect(x, cell.y, captionWidth, cell.height), caption, LabelColor);
        }
    }

    // Cell i of count across the rect, with gaps between them.
    private static Rect ListCellRect(Rect area, int index, int count)
    {
        float width = (area.width - ListCellGap * (count - 1)) / count;
        return new Rect(area.x + (width + ListCellGap) * index, area.y, width, area.height);
    }

    // Armor label, then sharp, blunt and heat with their icons on one row.
    private static void ArmorListRow(Rect area, ref float y, PawnReadout.Snapshot snapshot)
    {
        Rect row = NextStatRow(area, ref y);
        string label = TextCache.Key("VUIP.PawnPaneArmor");
        float labelWidth = ReadoutFont.Width(label) + 12f;
        ReadoutFont.Label(new Rect(row.x + 4f, row.y, labelWidth, row.height), label, LabelColor);
        Rect cells = new Rect(row.x + 4f + labelWidth, row.y, row.width - labelWidth - 8f, row.height);
        ArmorCell(ListCellRect(cells, 0, 3), 0, snapshot.ArmorSharpText, snapshot, 0x5C1A21);
        ArmorCell(ListCellRect(cells, 1, 3), 1, snapshot.ArmorBluntText, snapshot, 0x5C1A22);
        ArmorCell(ListCellRect(cells, 2, 3), 2, snapshot.ArmorHeatText, snapshot, 0x5C1A23);
    }

    private static void ArmorCell(Rect cell, int icon, string value, PawnReadout.Snapshot snapshot, int tipId)
    {
        ListCell(cell, icon, null, null, null, string.Empty, value, Color.white);
        Tip(cell, snapshot, ArmorTip, tipId);
    }

    // Movement, work speed and damage per second sharing one row.
    private static void StatListRow(Rect area, ref float y, PawnReadout.Snapshot snapshot)
    {
        UiPlusSettings settings = UiPlusMod.Settings;
        Rect row = NextStatRow(area, ref y);
        Rect cells = new Rect(row.x + 4f, row.y, row.width - 8f, row.height);
        int slot = 0;
        if (settings.pawnPaneShowSpeed)
        {
            Rect move = ListCellRect(cells, slot++, 3);
            ListCell(move, 4, null, null, null, TextCache.Key("VUIP.PawnPaneMovementShort"), snapshot.MoveText, Color.white);
            Tip(move, snapshot, s => StatTip(s.Pawn, StatDefOf.MoveSpeed, s.MoveSpeed), 0x5C1A03);
            Rect work = ListCellRect(cells, slot++, 3);
            ListCell(work, 5, null, null, null, TextCache.Key("VUIP.PawnPaneWorkSpeedShort"), snapshot.WorkText, Color.white);
            Tip(work, snapshot, s => StatTip(s.Pawn, StatDefOf.WorkSpeedGlobal, s.WorkSpeed), 0x5C1A09);
        }

        if (settings.pawnPaneShowDps)
        {
            Rect dps = ListCellRect(cells, slot, 3);
            ListCell(dps, 6, null, null, null, snapshot.Ranged ? TextCache.Key("VUIP.PawnPaneRangedDpsShort") : TextCache.Key("VUIP.PawnPaneMeleeDpsShort"),
                snapshot.DpsText, snapshot.Ranged && snapshot.Dps < 0f ? LabelColor : Color.white);
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

        float x = row.x + (hasIcon ? RowIconSpace : 4f);
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

        if (statCount == 0)
        {
            return y;
        }

        Rect statRow = NextStatRow(rect, ref y);
        Rect cells = new Rect(statRow.x + 4f, statRow.y, statRow.width - 8f, statRow.height);
        int slot = 0;
        for (int i = 0; i < view.Cards.Count; i++)
        {
            CreatureCard card = view.Cards[i];
            if (card.Line != CreatureLine.Stats)
            {
                continue;
            }

            Rect cell = ListCellRect(cells, slot++, Mathf.Max(statCount, 2));
            ListCell(cell, card.Icon, card.IconTexture, card.IconThing, null, card.Caption, card.Value, card.ValueColor);
            Tip(cell, view.Snapshot, card.Tip, card.TipId);
        }

        return y;
    }

    private enum CreatureLine
    {
        Own,
        Armor,
        Stats
    }
}
