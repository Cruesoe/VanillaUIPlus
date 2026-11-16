using RimWorld;
using UnityEngine;
using Verse;

namespace VanillaUIPlus;

/// <summary>
/// The Equipment box: one row per equipped weapon, then weapons carried in the inventory, each with its damage and range and the base game's buttons.
/// </summary>
public static partial class GearTabDrawer
{
    private const float EquipmentRowHeight = 44f;
    private const float EquipmentIconSize = 32f;

    private static float EquipmentBoxHeight(GearSnapshot snapshot)
    {
        return BoxHeight(snapshot.Equipment.Count > 0 ? snapshot.Equipment.Count * EquipmentRowHeight : LineHeight);
    }

    private static void DrawEquipmentBox(ITab_Pawn_Gear tab, GearSnapshot snapshot, Rect rect)
    {
        Rect area = BeginBox(rect, snapshot.EquipmentHeader);
        if (snapshot.Equipment.Count == 0)
        {
            DrawNote(new Rect(0f, area.y, area.width, LineHeight), snapshot.NoWeaponText);
        }

        for (int i = 0; i < snapshot.Equipment.Count; i++)
        {
            DrawEquipmentRow(tab, snapshot, new Rect(0f, area.y + i * EquipmentRowHeight, area.width, EquipmentRowHeight), snapshot.Equipment[i]);
        }

        Widgets.EndGroup();
    }

    // Icon with a health bar, label over the weapon's stats, then the buttons on the right: info, drop, and move to or from the inventory.
    private static void DrawEquipmentRow(ITab_Pawn_Gear tab, GearSnapshot snapshot, Rect row, GearItem item)
    {
        Thing thing = item.Thing;
        float x = row.xMax - ButtonSize;
        float buttonY = row.y + (row.height - ButtonSize) / 2f;
        Widgets.InfoCardButton(x, buttonY, thing);
        if (item.DropShown)
        {
            x -= ButtonSize;
            if (IconButton(new Rect(x, buttonY, ButtonSize, ButtonSize), TexButton.Drop, item.DropBlocked == null, item.DropTip))
            {
                GearItemMenu.Drop(tab, snapshot.Pawn, thing);
            }
        }

        if (item.Action != null)
        {
            x -= ButtonSize;
            GearRowAction action = item.Action;
            Texture2D texture = action.Kind == GearRowAction.ActionKind.UnequipToInventory ? TexButton.ReorderDown : TexButton.ReorderUp;
            if (IconButton(new Rect(x, buttonY, ButtonSize, ButtonSize), texture, action.Allowed, action.Tip))
            {
                GearItemMenu.Perform(snapshot.Pawn, thing, action);
            }
        }

        Rect body = new Rect(row.x, row.y, x - row.x - Gap, row.height);
        Rect icon = new Rect(body.x + 2f, body.y + 3f, EquipmentIconSize, EquipmentIconSize);
        Widgets.ThingIcon(icon, thing);
        GUI.color = Color.white;
        if (item.HitPoints >= 0f)
        {
            DrawHitPoints(new Rect(icon.x, icon.yMax + 2f, icon.width, 3f), item.HitPoints);
        }

        Rect text = new Rect(icon.xMax + 6f, body.y + 2f, body.xMax - icon.xMax - 6f, 20f);
        Text.Anchor = TextAnchor.MiddleLeft;
        GUI.color = item.Muted ? MutedText : Color.white;
        Text.WordWrap = false;
        Widgets.Label(text, item.Label.Truncate(text.width));
        if (item.Detail.Length > 0)
        {
            GUI.color = MutedText;
            Widgets.Label(new Rect(text.x, text.yMax, text.width, 20f), item.Detail.Truncate(text.width));
        }

        Text.WordWrap = true;
        GUI.color = Color.white;
        Text.Anchor = TextAnchor.UpperLeft;
        Highlight(body, item.Tip);
        if (item.Warning != null)
        {
            DrawWarning(new Rect(icon.x - 2f, icon.y - 2f, BadgeSize, BadgeSize), item.Warning);
        }

        if (Widgets.ButtonInvisible(body))
        {
            GearItemMenu.Open(tab, snapshot, item);
        }
    }
}
