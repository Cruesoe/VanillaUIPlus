using RimWorld;
using UnityEngine;
using Verse;

namespace VanillaUIPlus;

/// <summary>
/// The Inventory box: a grid of item cells with stack counts. Left click opens the info card; right click opens the item's menu.
/// </summary>
public static partial class GearTabDrawer
{
    private const float InventoryCellSize = 48f;
    private const float InventoryIconSize = 38f;

    private static int InventoryColumns(float width)
    {
        return Mathf.Max(1, Mathf.FloorToInt((BoxInnerWidth(width) + SlotGap) / (InventoryCellSize + SlotGap)));
    }

    private static float InventoryBoxHeight(GearSnapshot snapshot, float width)
    {
        int count = snapshot.Inventory.Count;
        if (count == 0)
        {
            return BoxHeight(LineHeight);
        }

        int columns = InventoryColumns(width);
        return BoxHeight((count + columns - 1) / columns * (InventoryCellSize + SlotGap));
    }

    private static void DrawInventoryBox(ITab_Pawn_Gear tab, GearSnapshot snapshot, Rect rect)
    {
        Rect area = BeginBox(rect, snapshot.InventoryHeader);
        if (snapshot.Inventory.Count == 0)
        {
            DrawNote(new Rect(0f, area.y, area.width, LineHeight), snapshot.EmptyText);
        }

        int columns = InventoryColumns(rect.width);
        for (int i = 0; i < snapshot.Inventory.Count; i++)
        {
            Rect cell = new Rect(i % columns * (InventoryCellSize + SlotGap), area.y + i / columns * (InventoryCellSize + SlotGap), InventoryCellSize, InventoryCellSize);
            DrawInventoryCell(tab, snapshot, cell, snapshot.Inventory[i]);
        }

        Widgets.EndGroup();
    }

    private static void DrawInventoryCell(ITab_Pawn_Gear tab, GearSnapshot snapshot, Rect cell, GearItem item)
    {
        Widgets.DrawOptionBackground(cell, false);
        Widgets.ThingIcon(new Rect(cell.center.x - InventoryIconSize / 2f, cell.y + 3f, InventoryIconSize, InventoryIconSize), item.Thing);
        GUI.color = Color.white;
        if (item.HitPoints >= 0f && item.HitPoints < 1f)
        {
            DrawHitPoints(new Rect(cell.x + 4f, cell.yMax - 5f, cell.width - 8f, 2f), item.HitPoints);
        }

        if (item.CountText.Length > 0)
        {
            Text.Anchor = TextAnchor.LowerRight;
            Widgets.Label(new Rect(cell.x, cell.y, cell.width - 3f, cell.height - 2f), item.CountText);
            Text.Anchor = TextAnchor.UpperLeft;
        }

        if (item.Held)
        {
            GUI.color = IconColor;
            GUI.DrawTexture(new Rect(cell.xMax - BadgeSize - 2f, cell.y + 2f, BadgeSize, BadgeSize), ForcedTex);
            GUI.color = Color.white;
        }

        if (item.Warning != null)
        {
            DrawWarning(new Rect(cell.x + 2f, cell.y + 2f, BadgeSize, BadgeSize), item.Warning);
        }

        Highlight(cell, item.Tip);
        if (!Widgets.ButtonInvisible(cell))
        {
            return;
        }

        if (Event.current.button == 1)
        {
            GearItemMenu.Open(tab, snapshot, item);
        }
        else
        {
            Find.WindowStack.Add(new Dialog_InfoCard(item.Thing));
        }
    }
}
