using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace VanillaUIPlus;

/// <summary>
/// Draws the improved Gear tab: a summary, the slot strip, then the base game's rows with an HP bar, warnings and inventory buttons.
/// Rows are drawn by the base game's DrawThingRow, so other mods' patches on it still apply.
/// </summary>
[StaticConstructorOnStartup]
public static class GearTabDrawer
{
    private const float TopPadding = 20f;
    private const float BaseHeight = 450f;
    private const float SummaryRowHeight = 24f;
    private const float SlotHeight = 34f;
    private const float MaxSlotWidth = 64f;
    private const float RowHeight = 28f;
    private const float ButtonSize = 24f;
    private const float ThingLeftX = 36f;
    private const float VanillaButtonsWidth = 24f + 24f + 24f + 60f;
    private const float Gap = 4f;

    private static readonly Texture2D LoadBarTex = SolidColorMaterials.NewSolidColorTexture(new Color(0.35f, 0.45f, 0.55f));
    private static readonly Texture2D LoadBarHeavyTex = SolidColorMaterials.NewSolidColorTexture(new Color(0.75f, 0.6f, 0.2f));
    private static readonly Texture2D LoadBarOverTex = SolidColorMaterials.NewSolidColorTexture(new Color(0.75f, 0.25f, 0.2f));
    private static readonly Texture2D HitPointsTex = SolidColorMaterials.NewSolidColorTexture(new Color(0.55f, 0.55f, 0.55f, 0.8f));
    private static readonly Texture2D HitPointsLowTex = SolidColorMaterials.NewSolidColorTexture(new Color(0.8f, 0.3f, 0.25f, 0.9f));
    private static readonly Color CellColor = new Color(1f, 1f, 1f, 0.05f);
    private static readonly Color EmptySlotColor = new Color(1f, 1f, 1f, 0.025f);
    private static readonly Color MutedText = new Color(0.6f, 0.6f, 0.6f);

    // Lifetime: the tab; only one Gear tab is open at a time.
    private static Vector2 scrollPosition;
    private static float scrollViewHeight;

    public static void Draw(ITab_Pawn_Gear tab)
    {
        Pawn pawn = GearTabAccess.SelPawnForGear(tab);
        ref Vector2 size = ref GearTabAccess.Size(tab);
        GearSnapshot snapshot = GearSnapshot.For(tab, pawn);

        Text.Font = GameFont.Small;
        Rect rect = new Rect(0f, TopPadding, size.x, size.y - TopPadding);
        Rect inner = rect.ContractedBy(10f);
        if (Prefs.DevMode && Widgets.ButtonText(new Rect(rect.xMax - 18f - 125f, 5f, 115f, Text.LineHeight), "Dev tool..."))
        {
            Find.WindowStack.Add(new FloatMenu(DebugToolsPawns.PawnGearDevOptions(pawn)));
        }

        Widgets.BeginGroup(inner);
        GUI.color = Color.white;
        float y = 0f;
        DrawSummary(snapshot, inner.width, ref y);
        DrawSlots(snapshot, inner.width, ref y);
        float headerHeight = y;

        Rect outRect = new Rect(0f, y, inner.width, inner.height - y);
        Rect viewRect = new Rect(0f, 0f, inner.width - 16f, scrollViewHeight);
        Widgets.BeginScrollView(outRect, ref scrollPosition, viewRect);
        float listY = 0f;
        DrawSection(tab, snapshot, snapshot.ShowEquipment, snapshot.EquipmentHeader, snapshot.EquipmentRows, false, viewRect.width, ref listY);
        DrawSection(tab, snapshot, snapshot.ShowEquipment && snapshot.UtilityRows.Count > 0, snapshot.UtilityHeader, snapshot.UtilityRows, false, viewRect.width, ref listY);
        DrawSection(tab, snapshot, snapshot.ShowApparel, snapshot.ApparelHeader, snapshot.ApparelRows, false, viewRect.width, ref listY);
        DrawSection(tab, snapshot, snapshot.ShowInventory, snapshot.InventoryHeader, snapshot.InventoryRows, true, viewRect.width, ref listY);

        if (Event.current.type == EventType.Layout)
        {
            // As the base game sizes the tab, capped by the current inspect pane height instead of the base game's.
            float wanted = headerHeight + listY + 70f;
            size.y = wanted > BaseHeight
                ? Mathf.Min(wanted, UI.screenHeight - 35f - PawnReadout.CurrentPaneHeight() - 30f)
                : BaseHeight;
            scrollViewHeight = listY + 20f;
        }

        Widgets.EndScrollView();
        Widgets.EndGroup();
        GUI.color = Color.white;
        Text.Anchor = TextAnchor.UpperLeft;
    }

    private static void DrawSummary(GearSnapshot snapshot, float width, ref float y)
    {
        if (snapshot.ShowLoad || snapshot.ShowTemperature)
        {
            Rect row = new Rect(0f, y, width, SummaryRowHeight);
            float half = (width - Gap) / 2f;
            if (snapshot.ShowLoad)
            {
                Rect bar = new Rect(row.x, row.y + 2f, half, row.height - 4f);
                float fraction = snapshot.LoadFraction;
                Texture2D fill = fraction > 1f ? LoadBarOverTex : fraction > 0.8f ? LoadBarHeavyTex : LoadBarTex;
                Widgets.FillableBar(bar, Mathf.Clamp01(fraction), fill, BaseContent.ClearTex, doBorder: true);
                Text.Anchor = TextAnchor.MiddleCenter;
                Widgets.Label(bar, snapshot.LoadText);
                Text.Anchor = TextAnchor.UpperLeft;
                Highlight(bar, snapshot.LoadTip);
            }

            if (snapshot.ShowTemperature)
            {
                Rect temperature = new Rect(row.x + half + Gap, row.y, width - half - Gap, row.height);
                Text.Anchor = TextAnchor.MiddleCenter;
                GUI.color = snapshot.TemperatureOutside ? ColorLibrary.RedReadable : Color.white;
                Widgets.Label(temperature, snapshot.TemperatureText);
                GUI.color = Color.white;
                Text.Anchor = TextAnchor.UpperLeft;
                Highlight(temperature, snapshot.TemperatureTip);
            }

            y += SummaryRowHeight + Gap;
        }

        if (snapshot.ShowArmor)
        {
            float cellWidth = (width - Gap * 2f) / 3f;
            for (int i = 0; i < 3; i++)
            {
                Rect cell = new Rect(i * (cellWidth + Gap), y, cellWidth, SummaryRowHeight);
                Widgets.DrawBoxSolid(cell, CellColor);
                Rect text = cell.ContractedBy(6f, 0f);
                Text.Anchor = TextAnchor.MiddleLeft;
                GUI.color = MutedText;
                Widgets.Label(text, snapshot.ArmorLabels[i]);
                GUI.color = Color.white;
                Text.Anchor = TextAnchor.MiddleRight;
                Widgets.Label(text, snapshot.ArmorTexts[i]);
                Text.Anchor = TextAnchor.UpperLeft;
                Highlight(cell, snapshot.ArmorTips[i]);
            }

            y += SummaryRowHeight + Gap;
        }

        if (snapshot.ShowWeapon)
        {
            Rect row = new Rect(0f, y, width, SummaryRowHeight);
            Widgets.DrawBoxSolid(row, CellColor);
            Text.Anchor = TextAnchor.MiddleLeft;
            Widgets.Label(row.ContractedBy(6f, 0f), snapshot.WeaponText);
            Text.Anchor = TextAnchor.UpperLeft;
            if (snapshot.WeaponTip != null)
            {
                Highlight(row, snapshot.WeaponTip);
            }

            y += SummaryRowHeight + Gap;
        }
    }

    private static void DrawSlots(GearSnapshot snapshot, float width, ref float y)
    {
        List<GearSlotState> slots = snapshot.Slots;
        if (slots.Count == 0)
        {
            return;
        }

        float slotWidth = Mathf.Min(MaxSlotWidth, (width - Gap * (slots.Count - 1)) / slots.Count);
        for (int i = 0; i < slots.Count; i++)
        {
            GearSlotState state = slots[i];
            Rect box = new Rect(i * (slotWidth + Gap), y, slotWidth, SlotHeight);
            Widgets.DrawBoxSolid(box, state.Item != null ? CellColor : EmptySlotColor);
            if (state.Problem != null)
            {
                GUI.color = ColorLibrary.RedReadable;
                Widgets.DrawBox(box);
                GUI.color = Color.white;
            }

            if (state.Item != null)
            {
                float icon = SlotHeight - 6f;
                Widgets.ThingIcon(new Rect(box.center.x - icon / 2f, box.y + 3f, icon, icon), state.Item);
            }
            else
            {
                Text.Anchor = TextAnchor.MiddleCenter;
                GUI.color = state.Problem != null ? ColorLibrary.RedReadable : MutedText;
                Widgets.Label(box, state.Slot.Label.Truncate(box.width - 2f));
                GUI.color = Color.white;
                Text.Anchor = TextAnchor.UpperLeft;
            }

            Highlight(box, snapshot.CanAct ? state.ClickTip : state.Tip);
            if (snapshot.CanAct && Widgets.ButtonInvisible(box))
            {
                SoundDefOf.Tick_High.PlayOneShotOnCamera();
                GearFillMenu.Open(snapshot.Pawn, state.Slot);
            }
        }

        y += SlotHeight + Gap * 2f;
    }

    private static void DrawSection(ITab_Pawn_Gear tab, GearSnapshot snapshot, bool show, string header, List<Thing> rows, bool inventory, float width, ref float y)
    {
        if (!show)
        {
            return;
        }

        Widgets.ListSeparator(ref y, width, header);
        for (int i = 0; i < rows.Count; i++)
        {
            Thing thing = rows[i];
            if (thing.Destroyed)
            {
                continue;
            }

            DrawRow(tab, snapshot, thing, inventory, width, ref y);
        }
    }

    private static void DrawRow(ITab_Pawn_Gear tab, GearSnapshot snapshot, Thing thing, bool inventory, float width, ref float y)
    {
        Pawn pawn = snapshot.Pawn;
        GearRowAction? action = snapshot.ActionFor(thing);
        Apparel? forceable = !inventory && snapshot.CanAct && thing is Apparel worn && pawn.outfits != null ? worn : null;
        float extra = (action != null ? ButtonSize : 0f) + (forceable != null ? ButtonSize : 0f);
        float rowWidth = width - extra;
        float rowY = y;
        GearTabAccess.DrawThingRow(tab, ref y, rowWidth, thing, inventory);

        float x = rowWidth;
        if (forceable != null)
        {
            Rect forceRect = new Rect(x, rowY + 2f, ButtonSize, ButtonSize);
            bool forced = pawn.outfits!.forcedHandler.IsForced(forceable);
            bool before = forced;
            Widgets.Checkbox(forceRect.position, ref forced, ButtonSize - 4f);
            TooltipHandler.TipRegion(forceRect, snapshot.ForceTip);
            if (forced != before)
            {
                pawn.outfits.forcedHandler.SetForced(forceable, forced);
                GearSnapshot.Invalidate();
            }

            x += ButtonSize;
        }

        if (action != null)
        {
            Rect buttonRect = new Rect(x, rowY + 2f, ButtonSize, ButtonSize);
            bool down = action.Kind == GearRowAction.ActionKind.RemoveToInventory || action.Kind == GearRowAction.ActionKind.UnequipToInventory;
            Color color = action.Allowed ? Color.white : Color.grey;
            TooltipHandler.TipRegion(buttonRect, action.Tip);
            if (Widgets.ButtonImage(buttonRect, down ? TexButton.ReorderDown : TexButton.ReorderUp, color, action.Allowed ? GenUI.MouseoverColor : color, action.Allowed)
                && action.Allowed)
            {
                Perform(pawn, thing, action);
            }
        }

        DrawHitPoints(thing, rowY, rowWidth);
        string? warning = snapshot.WarningFor(thing);
        if (warning != null)
        {
            Rect icon = new Rect(4f, rowY, RowHeight, RowHeight);
            GUI.DrawTexture(icon, WidgetsWork.WorkBoxOverlay_Warning);
            TooltipHandler.TipRegion(icon, warning);
        }
    }

    // A thin bar under the label; red below the base game's frayed apparel threshold.
    private static void DrawHitPoints(Thing thing, float rowY, float rowWidth)
    {
        if (!thing.def.useHitPoints || thing.MaxHitPoints <= 0)
        {
            return;
        }

        float fraction = Mathf.Clamp01((float)thing.HitPoints / thing.MaxHitPoints);
        float barWidth = rowWidth - ThingLeftX - VanillaButtonsWidth;
        if (barWidth <= 0f)
        {
            return;
        }

        Rect bar = new Rect(ThingLeftX, rowY + RowHeight - 3f, barWidth, 2f);
        GUI.DrawTexture(new Rect(bar.x, bar.y, bar.width * fraction, bar.height),
            fraction < ThoughtWorker_ApparelDamaged.MinForFrayed ? HitPointsLowTex : HitPointsTex);
    }

    private static void Perform(Pawn pawn, Thing thing, GearRowAction action)
    {
        switch (action.Kind)
        {
            case GearRowAction.ActionKind.RemoveToInventory:
                GearActions.OrderRemoveToInventory(pawn, (Apparel)thing);
                break;
            case GearRowAction.ActionKind.WearFromInventory:
                GearActions.OrderWearFromInventory(pawn, (Apparel)thing);
                break;
            case GearRowAction.ActionKind.UnequipToInventory:
                GearActions.UnequipToInventory(pawn, (ThingWithComps)thing);
                break;
            case GearRowAction.ActionKind.EquipFromInventory:
                GearActions.EquipFromInventory(pawn, thing);
                break;
        }
    }

    private static void Highlight(Rect rect, string tip)
    {
        Widgets.DrawHighlightIfMouseover(rect);
        TooltipHandler.TipRegion(rect, tip);
    }
}
