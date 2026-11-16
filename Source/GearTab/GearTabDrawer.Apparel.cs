using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace VanillaUIPlus;

/// <summary>
/// The Apparel box: every slot in a grid laid out by body, so an empty one shows what's missing, then worn apparel no slot takes.
/// </summary>
public static partial class GearTabDrawer
{
    private const int SlotColumns = 3;
    private const float SlotHeight = 58f;
    private const float SlotGap = 4f;
    private const float SlotIconSize = 40f;
    private const float BadgeSize = 16f;

    private static float ApparelBoxHeight(GearSnapshot snapshot, float width)
    {
        int rows = snapshot.SlotRows + (snapshot.OtherApparel.Count + SlotColumns - 1) / SlotColumns;
        return BoxHeight(rows > 0 ? rows * (SlotHeight + SlotGap) : LineHeight);
    }

    private static void DrawApparelBox(ITab_Pawn_Gear tab, GearSnapshot snapshot, Rect rect)
    {
        Rect area = BeginBox(rect, snapshot.ApparelHeader);
        float cellWidth = (area.width - SlotGap * (SlotColumns - 1)) / SlotColumns;
        if (snapshot.Slots.Count == 0 && snapshot.OtherApparel.Count == 0)
        {
            DrawNote(new Rect(0f, area.y, area.width, LineHeight), snapshot.EmptyText);
        }

        for (int i = 0; i < snapshot.Slots.Count; i++)
        {
            GearSlotState state = snapshot.Slots[i];
            int row = snapshot.RowMap[state.Slot.Row];
            DrawSlotCell(tab, snapshot, CellRect(area, cellWidth, state.Slot.Column, row), state, state.Item);
        }

        for (int i = 0; i < snapshot.OtherApparel.Count; i++)
        {
            Rect cell = CellRect(area, cellWidth, i % SlotColumns, snapshot.SlotRows + i / SlotColumns);
            DrawSlotCell(tab, snapshot, cell, null, snapshot.OtherApparel[i]);
        }

        Widgets.EndGroup();
    }

    private static Rect CellRect(Rect area, float cellWidth, int column, int row)
    {
        return new Rect(column * (cellWidth + SlotGap), area.y + row * (SlotHeight + SlotGap), cellWidth, SlotHeight);
    }

    // A slot (state set) or a worn item outside the slots (state null). Left click picks apparel for the slot; right click opens the item's menu.
    private static void DrawSlotCell(ITab_Pawn_Gear tab, GearSnapshot snapshot, Rect cell, GearSlotState? state, GearItem? item)
    {
        string? problem = state?.Problem;
        if (item != null)
        {
            Widgets.DrawOptionBackground(cell, false);
            DrawWornItem(cell, item, state?.MoreText ?? string.Empty);
        }
        else
        {
            Widgets.DrawBoxSolid(cell, EmptySlotColor);
            GUI.color = EmptySlotBorder;
            Widgets.DrawBox(cell);
            Text.Anchor = TextAnchor.MiddleCenter;
            GUI.color = problem != null ? ColorLibrary.RedReadable : MutedText;
            Widgets.Label(cell, state!.Slot.Label.Truncate(cell.width - 4f));
            GUI.color = Color.white;
            Text.Anchor = TextAnchor.UpperLeft;
        }

        if (problem != null)
        {
            GUI.color = ColorLibrary.RedReadable;
            Widgets.DrawBox(cell);
            GUI.color = Color.white;
        }

        string tip = state == null ? item!.Tip : snapshot.CanAct ? state.ClickTip : state.Tip;
        Highlight(cell, tip);
        if (!Widgets.ButtonInvisible(cell))
        {
            return;
        }

        if (Event.current.button == 1)
        {
            if (item != null)
            {
                GearItemMenu.Open(tab, snapshot, item);
            }
        }
        else if (state != null && snapshot.CanAct)
        {
            SoundDefOf.Tick_High.PlayOneShotOnCamera();
            GearFillMenu.Open(snapshot.Pawn, state.Slot);
        }
        else if (item != null)
        {
            GearItemMenu.Open(tab, snapshot, item);
        }
    }

    // The item's icon with a health bar along the bottom, forced and warning marks in the top corners, and a count of further items in the slot.
    private static void DrawWornItem(Rect cell, GearItem item, string moreText)
    {
        float icon = Mathf.Min(SlotIconSize, cell.width - 8f);
        Widgets.ThingIcon(new Rect(cell.center.x - icon / 2f, cell.y + 4f, icon, icon), item.Thing);
        GUI.color = Color.white;
        if (item.HitPoints >= 0f)
        {
            DrawHitPoints(new Rect(cell.x + 4f, cell.yMax - 7f, cell.width - 8f, 3f), item.HitPoints);
        }

        if (item.Forced)
        {
            GUI.color = IconColor;
            GUI.DrawTexture(new Rect(cell.xMax - BadgeSize - 2f, cell.y + 2f, BadgeSize, BadgeSize), ForcedTex);
            GUI.color = Color.white;
        }

        if (item.Warning != null)
        {
            DrawWarning(new Rect(cell.x + 2f, cell.y + 2f, BadgeSize, BadgeSize), item.Warning);
        }

        if (moreText.Length > 0)
        {
            Text.Anchor = TextAnchor.LowerRight;
            GUI.color = MutedText;
            Widgets.Label(new Rect(cell.x, cell.y, cell.width - 4f, cell.height - 8f), moreText);
            GUI.color = Color.white;
            Text.Anchor = TextAnchor.UpperLeft;
        }
    }
}
