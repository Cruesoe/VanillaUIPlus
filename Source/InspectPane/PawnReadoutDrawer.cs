using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace VanillaUIPlus;

/// <summary>
/// Draws the pawn inspect pane readout: the base game's bar row, stats on the left, skills on the right, and an activity line.
/// </summary>
[StaticConstructorOnStartup]
public static partial class PawnReadoutDrawer
{
    private const float SkillsWidth = 272f;
    private const float ColumnGap = 12f;
    private const float SkillGap = 8f;

    private const float LevelWidth = 22f;
    private const float PassionSize = 14f;

    private const float TopBarY = 3f;
    private const float BarHeight = 16f;

    // The activity and weapon labels are rebuilt this often rather than every frame.
    private const float FooterRefreshSeconds = 0.25f;

    private static readonly Color WarningColor = new Color(1f, 0.8f, 0.35f);
    private static readonly Color LabelColor = SettingsWidgets.MutedColor;

    private static readonly Color DividerColor = new Color(1f, 1f, 1f, 0.15f);
    private static readonly Color DisabledSkillColor = new Color(1f, 1f, 1f, 0.5f);

    // Same textures as the base game's inspect pane bars (InspectPaneFiller).
    private static readonly Texture2D BarBGTex = SolidColorMaterials.NewSolidColorTexture(new ColorInt(10, 10, 10).ToColor);
    private static readonly Texture2D HealthTex = SolidColorMaterials.NewSolidColorTexture(new ColorInt(35, 35, 35).ToColor);
    private static readonly Texture2D MoodTex = SolidColorMaterials.NewSolidColorTexture(new ColorInt(26, 52, 52).ToColor);

    // Reused for every top row bar so drawing allocates nothing; lives for the session.
    private static readonly WidgetRow BarRow = new WidgetRow();
    private static readonly Func<SkillRecord, string>? SkillDescription = ReflectionGuard.Delegate<Func<SkillRecord, string>>(
        nameof(SkillUI), "GetSkillDescription", AccessTools.Method(typeof(SkillUI), "GetSkillDescription"));

    private static readonly Texture2D SelfTendTex = ContentFinder<Texture2D>.Get("UI/VanillaUIPlus/SelfTend");
    private static readonly Texture2D SelfTendOffTex = ContentFinder<Texture2D>.Get("UI/VanillaUIPlus/SelfTendOff");
    private static readonly Color SelfTendOffColor = new Color(1f, 1f, 1f, 0.3f);
    private static readonly Texture2D NeedsViewTex = ContentFinder<Texture2D>.Get("UI/VanillaUIPlus/NeedsView");
    private static readonly Texture2D ArmorDurabilityTex = ContentFinder<Texture2D>.Get("UI/VanillaUIPlus/ArmorDurability");

    // Marks the top row's bars that open a menu.
    private static readonly Texture2D MenuArrowTex = ContentFinder<Texture2D>.Get("UI/VanillaUIPlus/DropdownArrow");
    private static readonly Color MenuArrowColor = new Color(1f, 1f, 1f, 0.6f);
    private const float MenuArrowSize = 10f;

    // Header line: gap holding the divider between the buttons and the pawn's details.
    private const float IdentityDividerGap = 9f;

    // Last age text measured for the header and its width; kept for the session (a short string only).
    private static string? ageWidthText;
    private static float ageWidth;

    private static readonly Dictionary<SkillDef, string> SkillLabels = new Dictionary<SkillDef, string>();

    private static Pawn? footerPawn;
    private static float footerTime = -1f;
    private static string? footerActivity;
    private static string? footerWeapon;

    // Self-tend toggle in the pane's header buttons, with the same rules as the Health tab's checkbox (HealthCardUtility).
    public static void DrawSelfTendButton(Pawn pawn, Rect paneRect, ref float lineEndWidth)
    {
        if (!pawn.IsColonist || pawn.playerSettings == null || pawn.DevelopmentalStage.Baby())
        {
            return;
        }

        Rect button = new Rect(paneRect.width - lineEndWidth - 24f, 0f, 24f, 24f);
        lineEndWidth += 24f;
        bool canDoctor = !pawn.WorkTypeIsDisabled(WorkTypeDefOf.Doctor);
        bool on = canDoctor && pawn.playerSettings.selfTend;
        if (Mouse.IsOver(button))
        {
            TooltipHandler.TipRegion(button, SelfTendTip(pawn, canDoctor, on));
        }

        if (!canDoctor)
        {
            GUI.color = SelfTendOffColor;
            GUI.DrawTexture(button.ContractedBy(3f), SelfTendOffTex);
            GUI.color = Color.white;
            return;
        }

        // Filled cross when on, outline when off.
        if (Widgets.ButtonImage(button.ContractedBy(3f), on ? SelfTendTex : SelfTendOffTex, Color.white, GenUI.MouseoverColor))
        {
            pawn.playerSettings.selfTend = !on;
            if (!on)
            {
                SoundDefOf.Checkbox_TurnedOn.PlayOneShotOnCamera();
                if (pawn.workSettings != null && pawn.workSettings.GetPriority(WorkTypeDefOf.Doctor) == 0)
                {
                    Messages.Message("MessageSelfTendUnsatisfied".Translate(pawn.LabelShort, pawn), MessageTypeDefOf.CautionInput, historical: false);
                }
            }
            else
            {
                SoundDefOf.Checkbox_TurnedOff.PlayOneShotOnCamera();
            }
        }
    }

    // Switches the stats column between stats and needs; the choice is saved for every pawn.
    public static void DrawNeedsViewButton(Pawn pawn, Rect paneRect, ref float lineEndWidth)
    {
        if (pawn.needs == null || !PawnReadout.IsOwn(pawn))
        {
            return;
        }

        Rect button = new Rect(paneRect.width - lineEndWidth - 24f, 0f, 24f, 24f);
        lineEndWidth += 24f;
        bool on = UiPlusMod.Settings.pawnPaneNeedsView;
        if (on)
        {
            Widgets.DrawHighlightSelected(button);
        }

        TooltipHandler.TipRegion(button, on ? TextCache.Key("VUIP.PawnPaneShowStats") : TextCache.Key("VUIP.PawnPaneShowNeeds"));
        if (Widgets.ButtonImage(button.ContractedBy(2f), NeedsViewTex, Color.white, GenUI.MouseoverColor))
        {
            UiPlusMod.Settings.pawnPaneNeedsView = !on;
            UiPlusMod.Instance.WriteSettings();
        }
    }

    // Same button and rules as the Bio tab's rename button (CharacterCardUtility): opens the name dialog, which also sets the title.
    public static void DrawRenameButton(Pawn pawn, Rect paneRect, ref float lineEndWidth)
    {
        if (!pawn.IsColonist && !pawn.IsColonySubhuman && !DebugSettings.ShowDevGizmos)
        {
            return;
        }

        Rect button = new Rect(paneRect.width - lineEndWidth - 24f, 0f, 24f, 24f);
        lineEndWidth += 24f;
        TooltipHandler.TipRegionByKey(button, "RenameColonist");
        // Full rect: the rename texture's symbol is shorter than the other header icons.
        if (Widgets.ButtonImage(button, TexButton.Rename))
        {
            Find.WindowStack.Add(pawn.NamePawnDialog());
        }
    }

    private static string SelfTendTip(Pawn pawn, bool canDoctor, bool on)
    {
        string state = on ? TextCache.Key("VUIP.PawnPaneSelfTendOn") : TextCache.Key("VUIP.PawnPaneSelfTendOff");
        return "AllowSelfTend".Translate().CapitalizeFirst().Resolve().AsTipTitle() + ": " + state + "\n\n"
            + (canDoctor
                ? "AllowSelfTendTip".Translate(Faction.OfPlayer.def.pawnsPlural, 0.7f.ToStringPercent()).CapitalizeFirst().Resolve()
                : "MessageCannotSelfTendEver".Translate(pawn.LabelShort, pawn).Resolve());
    }

    // Width of the header's age in the Medium font, measured only when the text changes.
    private static float AgeWidth(string age)
    {
        if (age != ageWidthText)
        {
            using (new TextBlock(GameFont.Medium))
            {
                ageWidth = Text.CalcSize(age).x;
            }

            ageWidthText = age;
        }

        return ageWidth;
    }

    private static string IdeoTip(Pawn pawn, Ideo ideo)
    {
        TaggedString tip = ideo.name.Colorize(ColoredText.TipSectionTitleColor)
            + "\n" + "Certainty".Translate().CapitalizeFirst() + ": " + pawn.ideo.Certainty.ToStringPercent()
            + "\n\n" + "ClickForMoreInfo".Translate().Colorize(ColoredText.SubtleGrayColor);
        if (pawn.ideo.PreviousIdeos.Any())
        {
            tip += "\n\n" + "Formerly".Translate().CapitalizeFirst() + ": \n" + pawn.ideo.PreviousIdeos.Select(x => x.name).ToLineList("  - ");
        }

        return tip.Resolve();
    }

    // Age, gender, xenotype and ideoligion (left to right) before a divider and the header buttons; tooltips and clicks match the Bio tab (CharacterCardUtility).
    // Laid out right to left, all at full white. Age and gender are information only; xenotype and ideoligion tint on hover like the header buttons.
    public static void DrawIdentityIcons(Pawn pawn, Rect paneRect, ref float lineEndWidth)
    {
        bool showAge = pawn.ageTracker != null;
        bool showGender = pawn.gender != Gender.None;
        bool showXenotype = ModsConfig.BiotechActive && pawn.genes != null && pawn.genes.GenesListForReading.Count > 0;
        Ideo? ideo = ModsConfig.IdeologyActive && !Find.IdeoManager.classicMode ? pawn.Ideo : null;
        if (!showAge && !showGender && !showXenotype && ideo == null)
        {
            return;
        }

        GUI.color = DividerColor;
        Widgets.DrawLineVertical(paneRect.width - lineEndWidth - IdentityDividerGap / 2f, 4f, 16f);
        GUI.color = Color.white;
        lineEndWidth += IdentityDividerGap;

        // Same conditions, tooltip and click as the Bio tab's ideoligion plate (IdeoUIUtility.DrawIdeoPlate).
        if (ideo != null)
        {
            Rect ideoRect = new Rect(paneRect.width - lineEndWidth - 24f, 0f, 24f, 24f);
            lineEndWidth += 24f;
            if (Mouse.IsOver(ideoRect))
            {
                TooltipHandler.TipRegion(ideoRect, () => IdeoTip(pawn, ideo), 0x5C1A13);
            }

            if (Widgets.ButtonImage(ideoRect.ContractedBy(3f), ideo.Icon, ideo.Color, GenUI.MouseoverColor))
            {
                IdeoUIUtility.OpenIdeoInfo(ideo);
            }
        }

        if (showXenotype)
        {
            Rect xenoRect = new Rect(paneRect.width - lineEndWidth - 24f, 0f, 24f, 24f);
            lineEndWidth += 24f;
            if (Mouse.IsOver(xenoRect))
            {
                TooltipHandler.TipRegion(xenoRect, () => ("Xenotype".Translate() + ": " + pawn.genes!.XenotypeLabelCap).Colorize(ColoredText.TipSectionTitleColor)
                    + "\n\n" + pawn.genes.XenotypeDescShort + "\n\n"
                    + "ViewGenesDesc".Translate(pawn.Named("PAWN")).ToString().StripTags().Colorize(ColoredText.SubtleGrayColor), 0x5C1A12);
            }

            if (Widgets.ButtonImage(xenoRect.ContractedBy(2f), pawn.genes!.XenotypeIcon, Color.white, GenUI.MouseoverColor))
            {
                InspectPaneUtility.OpenTab(typeof(ITab_Genes));
            }
        }

        if (showGender)
        {
            Rect genderRect = new Rect(paneRect.width - lineEndWidth - 24f, 0f, 24f, 24f);
            lineEndWidth += 24f;
            GUI.DrawTexture(genderRect.ContractedBy(2f), pawn.gender.GetIcon());
            if (Mouse.IsOver(genderRect))
            {
                TooltipHandler.TipRegion(genderRect, () => pawn.gender.GetLabel(pawn.AnimalOrWildMan()).CapitalizeFirst(), 0x5C1A11);
            }
        }

        if (showAge)
        {
            // Creatures can be days or quadrums old, so they get the base game's own age text rather than whole years.
            string age = pawn.RaceProps.Humanlike ? pawn.ageTracker!.AgeBiologicalYears.ToStringCached() : pawn.ageTracker!.AgeNumberString;
            // Medium font, top-anchored at the header's top like the pane's name (InspectPaneUtility.InspectPaneOnGUI), so the digits share its baseline.
            float width = AgeWidth(age) + 6f;
            Rect ageRect = new Rect(paneRect.width - lineEndWidth - width, 0f, width, 30f);
            lineEndWidth += width;
            Text.Font = GameFont.Medium;
            Text.Anchor = TextAnchor.UpperCenter;
            Widgets.Label(ageRect, age);
            Text.Anchor = TextAnchor.UpperLeft;
            Text.Font = GameFont.Small;
            if (Mouse.IsOver(ageRect))
            {
                TooltipHandler.TipRegion(ageRect, () => pawn.ageTracker.AgeTooltipString, 0x5C1A10);
            }
        }
    }

    public static void Draw(Pawn pawn, Rect rect)
    {
        Widgets.BeginGroup(rect);
        try
        {
            DrawTopBar(pawn, rect.width);
            PawnReadout.Snapshot snapshot = PawnReadout.For(pawn);
            Rect body = new Rect(0f, PawnReadout.TopBarHeight + PawnReadout.SectionGap, rect.width, PawnReadout.BodyHeight);
            Rect stats = body;
            if (UiPlusMod.Settings.pawnPaneShowSkills)
            {
                float skillsWidth = SkillsColumnWidth(body.width);
                Rect skills = new Rect(body.xMax - skillsWidth, body.y, skillsWidth, body.height);
                stats.xMax = skills.x - ColumnGap;
                GUI.color = DividerColor;
                Widgets.DrawLineVertical(skills.x - ColumnGap / 2f, body.y, body.height);
                GUI.color = Color.white;
                DrawSkills(pawn, skills);
            }

            if (PawnReadout.ShowingNeeds(pawn))
            {
                DrawNeeds(snapshot, stats);
            }
            else
            {
                DrawStats(snapshot, stats);
            }

            Rect footer = new Rect(0f, body.yMax + PawnReadout.SectionGap, rect.width, PawnReadout.FooterHeight);
            GUI.color = DividerColor;
            Widgets.DrawLineHorizontal(0f, footer.y - PawnReadout.SectionGap / 2f, rect.width);
            GUI.color = Color.white;
            DrawFooter(pawn, footer);
        }
        catch (Exception exception)
        {
            Log.ErrorOnce($"[Vanilla UI+] Error drawing pawn readout for {pawn}: {exception}", 0x5C1A7E);
        }
        finally
        {
            GUI.color = Color.white;
            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.UpperLeft;
            Text.WordWrap = true;
            Widgets.EndGroup();
        }
    }

    private static float SkillsColumnWidth(float width)
    {
        return Mathf.Min(SkillsWidth, width * 0.55f);
    }

    // The base game's first row (InspectPaneFiller.DoPaneContentsFor) in fixed slots: health, mood and schedule over the stat cards' three columns,
    // and the area, apparel policy and loadout menus over the skills. Without the skills column the row is split into equal halves.
    private static void DrawTopBar(Pawn pawn, float width)
    {
        float rightWidth = UiPlusMod.Settings.pawnPaneShowSkills ? SkillsColumnWidth(width) : (width - ColumnGap) / 2f;
        Rect left = new Rect(0f, TopBarY, width - rightWidth - ColumnGap, BarHeight);
        Rect right = new Rect(width - rightWidth, TopBarY, rightWidth, BarHeight);

        Bar(BarSlot(left, 0), pawn.health.summaryHealth.SummaryHealthPercent, HealthUtility.GetGeneralConditionLabel(pawn, shortVersion: true), false,
            UiPlusMod.Settings.pawnPaneColorHealthBar ? StatusBarColors.HealthTexture(pawn) : HealthTex, BarBGTex);

        if (pawn.needs?.mood != null)
        {
            Texture2D moodTex = UiPlusMod.Settings.pawnPaneColorMoodBar && pawn.mindState?.mentalBreaker != null ? StatusBarColors.MoodTexture(pawn) : MoodTex;
            Bar(BarSlot(left, 1), pawn.needs.mood.CurLevelPercentage, pawn.needs.mood.MoodString.CapitalizeFirst(), false, moodTex, BarBGTex);
        }

        if (pawn.timetable != null && !pawn.IsPrisonerOfColony)
        {
            TimeAssignmentDef assignment = pawn.timetable.CurrentAssignment;
            Bar(BarSlot(left, 2), 1f, assignment.LabelCap, false, assignment.ColorTexture);
        }

        DrawAreaAllowed(pawn, BarSlot(right, 0));
        DrawApparelPolicy(pawn, BarSlot(right, 1));
        if (PolicyBarShown(pawn) && CombatExtendedLoadouts.Available && CombatExtendedLoadouts.Loadout(pawn) is { } loadout)
        {
            DrawLoadout(pawn, loadout, BarSlot(right, 2));
        }
    }

    // One of three equal slots across a half, with the stat cards' gap between them.
    private static Rect BarSlot(Rect half, int index)
    {
        float slotWidth = (half.width - CardGap * 2f) / 3f;
        return new Rect(half.x + (slotWidth + CardGap) * index, half.y, slotWidth, half.height);
    }

    // Draws through WidgetRow.FillableBar so the label matches the base game's bars; a bar with a menu leaves room for its arrow.
    private static Rect Bar(Rect slot, float fillPct, string label, bool menu, Texture2D fillTex, Texture2D? bgTex = null)
    {
        BarRow.Init(slot.x, slot.y, UIDirection.RightThenUp, 99999f, 0f);
        Rect rect = BarRow.FillableBar(slot.width, slot.height, fillPct, BarLabel(label, slot.width, menu), fillTex, bgTex);
        if (menu)
        {
            DrawMenuArrow(rect);
        }

        return rect;
    }

    private static bool PolicyBarShown(Pawn pawn)
    {
        return pawn.outfits != null && pawn.Faction == Faction.OfPlayer && pawn.HostFaction == null;
    }

    // Bar labels use the tiny font (WidgetRow.FillableBar), cut to the bar's width less any menu arrow.
    private static string BarLabel(string label, float barWidth, bool menu)
    {
        using (new TextBlock(GameFont.Tiny))
        {
            return label.Truncate(barWidth - 4f - (menu ? MenuArrowSize : 0f));
        }
    }

    // Same conditions, hover and menu as the base game's area bar (InspectPaneFiller.DrawAreaAllowed).
    private static void DrawAreaAllowed(Pawn pawn, Rect slot)
    {
        Pawn_PlayerSettings? settings = pawn.playerSettings;
        if (settings == null || !settings.SupportsAllowedAreas || pawn.Faction != Faction.OfPlayer || pawn.HostFaction != null
            || (pawn.IsMutant && !pawn.mutant.Def.respectsAllowedArea))
        {
            return;
        }

        Area? area = settings.AreaRestrictionInPawnCurrentMap;
        Rect rect = Bar(slot, 1f, AreaUtility.AreaAllowedLabel(pawn), true, area != null ? area.ColorTexture : BaseContent.GreyTex);
        if (Mouse.IsOver(rect))
        {
            area?.MarkForDraw();
            Widgets.DrawBox(rect.ContractedBy(-1f));
        }

        if (Widgets.ButtonInvisible(rect))
        {
            AreaUtility.MakeAllowedAreaListFloatMenu(a => pawn.playerSettings.AreaRestrictionInPawnCurrentMap = a, addNullAreaOption: true, addManageOption: true, pawn.MapHeld);
        }
    }

    // Down arrow at the right end of a bar that opens a menu when clicked; brighter on hover.
    private static void DrawMenuArrow(Rect bar)
    {
        Rect arrow = new Rect(bar.xMax - MenuArrowSize - 2f, bar.y + (bar.height - MenuArrowSize) / 2f, MenuArrowSize, MenuArrowSize);
        GUI.color = Mouse.IsOver(bar) ? Color.white : MenuArrowColor;
        GUI.DrawTexture(arrow, MenuArrowTex);
        GUI.color = Color.white;
    }

    // Apparel policy bar; quest lodgers' policy can't be changed, so theirs has no menu. The menu matches the Assign tab's apparel column (PawnColumnWorker_Outfit).
    private static void DrawApparelPolicy(Pawn pawn, Rect slot)
    {
        if (pawn.outfits?.CurrentApparelPolicy == null || !PolicyBarShown(pawn))
        {
            return;
        }

        ApparelPolicy policy = pawn.outfits.CurrentApparelPolicy;
        bool lodger = pawn.IsQuestLodger();
        Rect rect = Bar(slot, 1f, policy.label, !lodger, BaseContent.GreyTex);
        if (Mouse.IsOver(rect))
        {
            TooltipHandler.TipRegion(rect, lodger
                ? "QuestRelated_Outfit".Translate().Resolve()
                : "VUIP.PawnPaneApparelPolicyTip".Translate(policy.label).Resolve());
            if (!lodger)
            {
                Widgets.DrawBox(rect.ContractedBy(-1f));
            }
        }

        if (lodger || !Widgets.ButtonInvisible(rect))
        {
            return;
        }

        List<FloatMenuOption> options = new List<FloatMenuOption>();
        foreach (ApparelPolicy each in Current.Game.outfitDatabase.AllOutfits)
        {
            options.Add(new FloatMenuOption(each.label, () => pawn.outfits.CurrentApparelPolicy = each));
        }

        options.Add(new FloatMenuOption($"{"AssignTabEdit".Translate()}...",
            () => Find.WindowStack.Add(new Dialog_ManageApparelPolicies(pawn.outfits.CurrentApparelPolicy))));
        Find.WindowStack.Add(new FloatMenu(options));
    }

    // Combat Extended loadout bar; the menu matches CE's Assign tab loadout column.
    private static void DrawLoadout(Pawn pawn, object loadout, Rect slot)
    {
        string label = CombatExtendedLoadouts.Label(loadout);
        Rect rect = Bar(slot, 1f, label, true, BaseContent.GreyTex);
        if (Mouse.IsOver(rect))
        {
            TooltipHandler.TipRegion(rect, "VUIP.PawnPaneLoadoutTip".Translate(label).Resolve());
            Widgets.DrawBox(rect.ContractedBy(-1f));
        }

        if (!Widgets.ButtonInvisible(rect))
        {
            return;
        }

        List<FloatMenuOption> options = new List<FloatMenuOption>();
        foreach (object each in CombatExtendedLoadouts.All())
        {
            options.Add(new FloatMenuOption(CombatExtendedLoadouts.Label(each), () => CombatExtendedLoadouts.Assign(pawn, each)));
        }

        options.Add(new FloatMenuOption($"{"AssignTabEdit".Translate()}...", () =>
        {
            if (CombatExtendedLoadouts.ManageDialog(CombatExtendedLoadouts.Loadout(pawn) ?? loadout) is { } dialog)
            {
                Find.WindowStack.Add(dialog);
            }
        }));
        Find.WindowStack.Add(new FloatMenu(options));
    }

    private static void DrawSkills(Pawn pawn, Rect rect)
    {
        if (pawn.DevelopmentalStage.Baby())
        {
            GUI.color = Color.gray;
            Text.Anchor = TextAnchor.MiddleCenter;
            Widgets.Label(rect, TextCache.Key("SkillsDevelopLaterBaby"));
            Text.Anchor = TextAnchor.UpperLeft;
            GUI.color = Color.white;
            return;
        }

        List<SkillDef> skills = PawnReadout.SkillsInOrder;
        float cellWidth = (rect.width - SkillGap) / 2f;
        float rowHeight = rect.height / Mathf.Max(PawnReadout.StatRows, PawnReadout.SkillRows);
        Widgets.DrawBoxSolid(new Rect(rect.x + cellWidth + SkillGap / 2f, rect.y, 1f, rect.height), DividerColor);
        for (int i = 0; i < skills.Count; i++)
        {
            SkillRecord? skill = pawn.skills.GetSkill(skills[i]);
            if (skill == null)
            {
                continue;
            }

            Rect cell = new Rect(rect.x + (i % 2) * (cellWidth + SkillGap), rect.y + (i / 2) * rowHeight, cellWidth, rowHeight);
            if (cell.yMax > rect.yMax + 0.5f)
            {
                break;
            }

            DrawSkill(skill, cell);
        }
    }

    // Skill rows keep the Bio tab's passion icons and tooltips, with aptitude indicated at the left edge.
    private static void DrawSkill(SkillRecord skill, Rect cell)
    {
        Widgets.DrawHighlightIfMouseover(cell);
        Widgets.DrawBoxSolid(new Rect(cell.x, cell.yMax - 1f, cell.width, 1f), DividerColor * new Color(1f, 1f, 1f, 0.65f));
        int level = skill.GetLevel();
        bool disabled = skill.TotallyDisabled;
        if (!disabled && (ModsConfig.BiotechActive || ModsConfig.AnomalyActive) && skill.Aptitude != 0)
        {
            Widgets.DrawBoxSolid(new Rect(cell.x, cell.y + 5f, 2f, cell.height - 10f),
                skill.Aptitude > 0 ? TemperatureGreen : TemperatureRed);
        }

        Text.Font = GameFont.Small;
        Text.Anchor = TextAnchor.MiddleLeft;
        GUI.color = disabled ? DisabledSkillColor : Color.white;
        Rect labelRect = new Rect(cell.x + 4f, cell.y, cell.width - LevelWidth - PassionSize - 8f, cell.height);
        Widgets.Label(labelRect, TextCache.Truncate(SkillLabel(skill.def), labelRect.width));

        if (!disabled && skill.passion != Passion.None && PassionIcons.For(skill.passion) is Texture2D passionIcon)
        {
            Rect passionRect = new Rect(cell.xMax - LevelWidth - PassionSize - 2f, cell.y + (cell.height - PassionSize) / 2f, PassionSize, PassionSize);
            GUI.DrawTexture(passionRect, passionIcon);
        }

        Text.Anchor = TextAnchor.MiddleRight;
        Widgets.Label(new Rect(cell.xMax - LevelWidth - 2f, cell.y, LevelWidth, cell.height), disabled ? "-" : level.ToStringCached());
        Text.Anchor = TextAnchor.UpperLeft;
        GUI.color = Color.white;

        if (SkillDescription != null && Mouse.IsOver(cell))
        {
            TooltipHandler.TipRegion(cell, new TipSignal(() => SkillTooltip.Append(SkillDescription(skill), skill), skill.def.GetHashCode() * 397945));
        }
    }

    private static string SkillLabel(SkillDef def)
    {
        EnsureLanguage();
        if (!SkillLabels.TryGetValue(def, out string? label))
        {
            label = def.skillLabel.CapitalizeFirst();
            SkillLabels[def] = label;
        }

        return label;
    }

    // Current activity and weapon on one line; the tooltip has the base game's full inspect text.
    private static void DrawFooter(Pawn pawn, Rect rect)
    {
        RefreshFooter(pawn);
        Widgets.DrawHighlightIfMouseover(rect);
        Text.Font = GameFont.Small;
        Rect inner = new Rect(rect.x + 4f, rect.y, rect.width - 6f, rect.height);
        float weaponWidth = 0f;
        if (!footerWeapon.NullOrEmpty())
        {
            // Weapon on the right, given up to 45% of the line; the activity takes what is left.
            string shownWeapon = TextCache.Truncate(footerWeapon, inner.width * 0.45f);
            weaponWidth = TextCache.Width(shownWeapon);
            Text.Anchor = TextAnchor.MiddleRight;
            Widgets.Label(inner, shownWeapon);
        }

        if (!footerActivity.NullOrEmpty())
        {
            float activityWidth = inner.width - (weaponWidth > 0f ? weaponWidth + 12f : 0f);
            Text.Anchor = TextAnchor.MiddleLeft;
            Widgets.Label(new Rect(inner.x, inner.y, activityWidth, inner.height), TextCache.Truncate(footerActivity, activityWidth));
        }

        Text.Anchor = TextAnchor.UpperLeft;
        if (Mouse.IsOver(rect))
        {
            TooltipHandler.TipRegion(rect, new TipSignal(() => InspectText(pawn), 0x5C1A08));
        }
    }

    // Drops the footer's cached pawn so an unloaded game isn't kept in memory.
    public static void Forget()
    {
        footerPawn = null;
        footerActivity = null;
        footerWeapon = null;
    }

    private static void RefreshFooter(Pawn pawn)
    {
        float now = Time.realtimeSinceStartup;
        if (pawn == footerPawn && now - footerTime < FooterRefreshSeconds && now >= footerTime)
        {
            return;
        }

        footerPawn = pawn;
        footerTime = now;
        footerActivity = pawn.InMentalState
            ? pawn.MentalStateDef.LabelCap.Resolve()
            : pawn.jobs?.curDriver?.GetReport()?.CapitalizeFirst();
        footerWeapon = pawn.equipment?.Primary?.LabelCap;
    }

    private static string CombatTip(PawnReadout.Snapshot snapshot)
    {
        if (!snapshot.Ranged)
        {
            return StatTip(snapshot.Pawn, StatDefOf.MeleeDPS, snapshot.Dps) + "\n\n" + StatTip(snapshot.Pawn, StatDefOf.MeleeHitChance, snapshot.HitChance);
        }

        string dps = snapshot.RangedTip ?? TextCache.Key("VUIP.PawnPaneRangedDpsUnknown");
        return snapshot.HitTip == null ? dps : dps + "\n\n" + snapshot.HitTip;
    }

    private static string ArmorTip(PawnReadout.Snapshot snapshot)
    {
        if (PawnReadout.CombatExtendedActive)
        {
            return "OverallArmor".Translate().Resolve().AsTipTitle() + "\n\n"
                + "ArmorSharp".Translate() + ": " + PawnReadout.CombatExtendedValue(snapshot.ArmorSharp, "CE_mmRHA") + "\n"
                + "ArmorBlunt".Translate() + ": " + PawnReadout.CombatExtendedValue(snapshot.ArmorBlunt, "CE_MPa") + "\n"
                + "ArmorHeat".Translate() + ": " + snapshot.ArmorHeat.ToStringPercent() + "\n\n"
                + "VUIP.PawnPaneArmorTipCombatExtended".Translate().Resolve().Colorize(ColoredText.SubtleGrayColor);
        }

        return "OverallArmor".Translate().Resolve().AsTipTitle() + "\n\n"
            + "ArmorSharp".Translate() + ": " + snapshot.ArmorSharp.ToStringPercent() + "\n"
            + "ArmorBlunt".Translate() + ": " + snapshot.ArmorBlunt.ToStringPercent() + "\n"
            + "ArmorHeat".Translate() + ": " + snapshot.ArmorHeat.ToStringPercent() + "\n\n"
            + "VUIP.PawnPaneArmorTip".Translate().Resolve().Colorize(ColoredText.SubtleGrayColor);
    }

    private static string StatTip(Pawn pawn, StatDef stat, float value)
    {
        return stat.LabelCap.Resolve().AsTipTitle() + "\n\n" + stat.Worker.GetExplanationFull(StatRequest.For(pawn), stat.toStringNumberSense, value);
    }

    private static string InspectText(Pawn pawn)
    {
        try
        {
            string text = pawn.GetInspectString();
            string lowPriority = pawn.GetInspectStringLowPriority();
            if (!lowPriority.NullOrEmpty())
            {
                text = text.NullOrEmpty() ? lowPriority : text.TrimEndNewlines() + "\n" + lowPriority;
            }

            return text;
        }
        catch (Exception exception)
        {
            return exception.Message;
        }
    }
}
