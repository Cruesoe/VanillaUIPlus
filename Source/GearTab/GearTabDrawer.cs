using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace VanillaUIPlus;

/// <summary>
/// Draws the improved Gear tab in the base game's section boxes: Combat and Protection across the top, Apparel on the left,
/// Equipment and Inventory on the right, and the load bar along the bottom.
/// </summary>
[StaticConstructorOnStartup]
public static partial class GearTabDrawer
{
    // Wide enough for two columns of boxes; the base game's tab is 460 by 450.
    public const float TabWidth = 620f;
    public const float VanillaWidth = 460f;
    private const float TopPadding = 20f;
    private const float BaseHeight = 450f;
    private const float LoadHeight = 24f;
    private const float StatRowHeight = 24f;
    private const float StatIconSize = 18f;
    private const float BoxPadding = 6f;
    // Height Widgets.ListSeparator adds: 3 above, 20 for the label, 2 under the line.
    private const float BoxTitleHeight = 25f;
    private const float LineHeight = 24f;
    private const float ButtonSize = 24f;
    private const float Gap = 4f;

    private static readonly Texture2D LoadBarTex = SolidColorMaterials.NewSolidColorTexture(new Color(0.35f, 0.45f, 0.55f));
    private static readonly Texture2D LoadBarHeavyTex = SolidColorMaterials.NewSolidColorTexture(new Color(0.75f, 0.6f, 0.2f));
    private static readonly Texture2D LoadBarOverTex = SolidColorMaterials.NewSolidColorTexture(new Color(0.75f, 0.25f, 0.2f));
    private static readonly Texture2D HitPointsTex = SolidColorMaterials.NewSolidColorTexture(new Color(0.55f, 0.55f, 0.55f, 0.8f));
    private static readonly Texture2D HitPointsLowTex = SolidColorMaterials.NewSolidColorTexture(new Color(0.8f, 0.3f, 0.25f, 0.9f));
    private static readonly Texture2D HitPointsBackTex = SolidColorMaterials.NewSolidColorTexture(new Color(0f, 0f, 0f, 0.4f));
    private static readonly Color EmptySlotColor = new Color(1f, 1f, 1f, 0.025f);
    private static readonly Color EmptySlotBorder = new Color(1f, 1f, 1f, 0.08f);
    private static readonly Color MutedText = new Color(0.6f, 0.6f, 0.6f);
    private static readonly Color IconColor = new Color(0.8f, 0.8f, 0.8f);
    internal static readonly Texture2D MeleeTex = ContentFinder<Texture2D>.Get("UI/Icons/MainButtons/shield-sword");
    internal static readonly Texture2D RangeTex = ContentFinder<Texture2D>.Get("UI/Icons/MainButtons/ruler");
    private static readonly Texture2D ForcedTex = ContentFinder<Texture2D>.Get("UI/Icons/MainButtons/flag-variant");

    // Lifetime: the tab; only one Gear tab is open at a time.
    private static Vector2 scrollPosition;
    private static float contentHeight;

    public static void Draw(ITab_Pawn_Gear tab)
    {
        Pawn pawn = GearTabAccess.SelPawnForGear(tab);
        ref Vector2 size = ref GearTabAccess.Size(tab);
        GearSnapshot snapshot = GearSnapshot.For(tab, pawn);
        size.x = TabWidth;

        Text.Font = GameFont.Small;
        Rect rect = new Rect(0f, TopPadding, size.x, size.y - TopPadding);
        Rect inner = rect.ContractedBy(10f);
        if (Prefs.DevMode && Widgets.ButtonText(new Rect(rect.xMax - 18f - 125f, 5f, 115f, Text.LineHeight), "Dev tool..."))
        {
            Find.WindowStack.Add(new FloatMenu(DebugToolsPawns.PawnGearDevOptions(pawn)));
        }

        Widgets.BeginGroup(inner);
        GUI.color = Color.white;
        float top = 0f;
        DrawStatBoxes(snapshot, inner.width, ref top);
        float loadSpace = snapshot.ShowLoad ? LoadHeight + Gap : 0f;
        if (snapshot.ShowLoad)
        {
            DrawLoad(snapshot, new Rect(0f, inner.height - LoadHeight, inner.width, LoadHeight));
        }

        Rect outRect = new Rect(0f, top, inner.width, Mathf.Max(0f, inner.height - top - loadSpace));
        float viewWidth = contentHeight > outRect.height + 1f ? outRect.width - 16f : outRect.width;
        Rect viewRect = new Rect(0f, 0f, viewWidth, Mathf.Max(contentHeight, outRect.height));
        Widgets.BeginScrollView(outRect, ref scrollPosition, viewRect);
        float height = DrawColumns(tab, snapshot, viewWidth);
        if (Event.current.type == EventType.Layout)
        {
            contentHeight = height;
            // As the base game sizes the tab, capped by the current inspect pane height instead of the base game's.
            float wanted = TopPadding + 20f + top + height + loadSpace + 10f;
            size.y = wanted > BaseHeight ? Mathf.Min(wanted, UI.screenHeight - 35f - PawnReadout.CurrentPaneHeight() - 30f) : BaseHeight;
        }

        Widgets.EndScrollView();
        Widgets.EndGroup();
        GUI.color = Color.white;
        Text.Anchor = TextAnchor.UpperLeft;
    }

    // Apparel on the left, Equipment over Inventory on the right; one full-width column when a side has nothing. Returns the height used.
    private static float DrawColumns(ITab_Pawn_Gear tab, GearSnapshot snapshot, float width)
    {
        bool left = snapshot.ShowApparel;
        bool right = snapshot.ShowEquipment || snapshot.ShowInventory;
        float columnWidth = left && right ? (width - Gap) / 2f : width;
        float leftHeight = 0f;
        if (left)
        {
            leftHeight = ApparelBoxHeight(snapshot, columnWidth);
            DrawApparelBox(tab, snapshot, new Rect(0f, 0f, columnWidth, leftHeight));
        }

        float x = left && right ? columnWidth + Gap : 0f;
        float rightHeight = 0f;
        if (snapshot.ShowEquipment)
        {
            float height = EquipmentBoxHeight(snapshot);
            DrawEquipmentBox(tab, snapshot, new Rect(x, 0f, columnWidth, height));
            rightHeight = height + Gap;
        }

        if (snapshot.ShowInventory)
        {
            float height = InventoryBoxHeight(snapshot, columnWidth);
            DrawInventoryBox(tab, snapshot, new Rect(x, rightHeight, columnWidth, height));
            rightHeight += height;
        }

        return Mathf.Max(leftHeight, rightHeight);
    }

    // Draws the base game's section frame and title; returns the area under the title, in the box's own group, which the caller must end.
    private static Rect BeginBox(Rect rect, string title)
    {
        Widgets.DrawMenuSection(rect);
        Rect inner = rect.ContractedBy(BoxPadding);
        Widgets.BeginGroup(inner);
        float y = 0f;
        Widgets.ListSeparator(ref y, inner.width, title);
        return new Rect(0f, y, inner.width, inner.height - y);
    }

    private static float BoxHeight(float content)
    {
        return BoxPadding * 2f + BoxTitleHeight + content;
    }

    private static float BoxInnerWidth(float width)
    {
        return width - BoxPadding * 2f;
    }

    // Combat and Protection side by side; a lone box takes the full width.
    private static void DrawStatBoxes(GearSnapshot snapshot, float width, ref float y)
    {
        List<GearStat> combat = snapshot.Combat;
        List<GearStat> protection = snapshot.Protection;
        int rows = Mathf.Max(combat.Count, protection.Count);
        if (rows == 0)
        {
            return;
        }

        float height = BoxHeight(rows * StatRowHeight);
        float boxWidth = combat.Count > 0 && protection.Count > 0 ? (width - Gap) / 2f : width;
        float x = 0f;
        if (combat.Count > 0)
        {
            DrawStatBox(new Rect(x, y, boxWidth, height), snapshot.CombatHeader, combat);
            x += boxWidth + Gap;
        }

        if (protection.Count > 0)
        {
            DrawStatBox(new Rect(x, y, boxWidth, height), snapshot.ProtectionHeader, protection);
        }

        y += height + Gap;
    }

    private static void DrawStatBox(Rect rect, string title, List<GearStat> stats)
    {
        Rect area = BeginBox(rect, title);
        for (int i = 0; i < stats.Count; i++)
        {
            DrawStatRow(new Rect(0f, area.y + i * StatRowHeight, area.width, StatRowHeight), stats[i]);
        }

        Widgets.EndGroup();
    }

    // Icon, muted label and colored value, in the inspect pane's list style.
    private static void DrawStatRow(Rect row, GearStat stat)
    {
        Rect icon = new Rect(row.x + 2f, row.y + (row.height - StatIconSize) / 2f, StatIconSize, StatIconSize);
        if (stat.Texture != null)
        {
            GUI.color = IconColor;
            GUI.DrawTexture(icon, stat.Texture, ScaleMode.ScaleToFit);
            GUI.color = Color.white;
        }
        else if (stat.Icon >= 0)
        {
            PawnReadoutDrawer.StatIcon(icon, stat.Icon);
        }

        Rect text = new Rect(icon.xMax + 6f, row.y, row.xMax - icon.xMax - 10f, row.height);
        Text.Anchor = TextAnchor.MiddleLeft;
        GUI.color = MutedText;
        Widgets.Label(text, stat.Label);
        Text.Anchor = TextAnchor.MiddleRight;
        GUI.color = stat.Color;
        Widgets.Label(text, stat.Value);
        GUI.color = Color.white;
        Text.Anchor = TextAnchor.UpperLeft;
        Highlight(row, stat.Tip);
    }

    // Mass carried against capacity, colored as the pawn nears and passes it.
    private static void DrawLoad(GearSnapshot snapshot, Rect bar)
    {
        float fraction = snapshot.LoadFraction;
        Texture2D fill = fraction > 1f ? LoadBarOverTex : fraction > 0.8f ? LoadBarHeavyTex : LoadBarTex;
        Widgets.FillableBar(bar, Mathf.Clamp01(fraction), fill, BaseContent.ClearTex, doBorder: true);
        Text.Anchor = TextAnchor.MiddleCenter;
        Widgets.Label(bar, snapshot.LoadText);
        Text.Anchor = TextAnchor.UpperLeft;
        Highlight(bar, snapshot.LoadTip);
    }

    // A muted line for an empty box.
    private static void DrawNote(Rect rect, string text)
    {
        Text.Anchor = TextAnchor.MiddleLeft;
        GUI.color = MutedText;
        Widgets.Label(rect, text);
        GUI.color = Color.white;
        Text.Anchor = TextAnchor.UpperLeft;
    }

    // A thin health bar; red below the base game's frayed apparel threshold.
    private static void DrawHitPoints(Rect bar, float fraction)
    {
        GUI.DrawTexture(bar, HitPointsBackTex);
        GUI.DrawTexture(new Rect(bar.x, bar.y, bar.width * fraction, bar.height), fraction < ThoughtWorker_ApparelDamaged.MinForFrayed ? HitPointsLowTex : HitPointsTex);
    }

    private static void DrawWarning(Rect rect, string warning)
    {
        GUI.DrawTexture(rect, WidgetsWork.WorkBoxOverlay_Warning);
        TooltipHandler.TipRegion(rect, warning);
    }

    // A small icon button, greyed out and inert when not allowed; returns true when clicked and allowed.
    private static bool IconButton(Rect rect, Texture2D texture, bool allowed, string tip)
    {
        TooltipHandler.TipRegion(rect, tip);
        Color color = allowed ? Color.white : Color.grey;
        return Widgets.ButtonImage(rect, texture, color, allowed ? GenUI.MouseoverColor : color, allowed) && allowed;
    }

    // Tooltips are left out while a menu is open: menus open beside the cursor, so a tooltip would cover them.
    private static void Highlight(Rect rect, string tip)
    {
        Widgets.DrawHighlightIfMouseover(rect);
        if (Find.WindowStack.FloatMenu == null)
        {
            TooltipHandler.TipRegion(rect, tip);
        }
    }
}
