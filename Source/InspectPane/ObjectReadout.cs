using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace VanillaUIPlus;

/// <summary>
/// Inspect pane cards for a selected workbench, powered or fueled building, item, storage building or stockpile: which selection gets them, the pane size,
/// and a cached view of the cards and the inspect text they leave.
/// </summary>
[StaticConstructorOnStartup]
public static partial class ObjectReadout
{
    private const float RefreshSeconds = 1f;
    // Where the base game's pane starts a thing's text, under its health bar row (InspectPaneFiller.DoPaneContentsFor).
    private const float ThingTop = 21f;
    private const int ContentsTipLines = 12;

    private static readonly Texture2D BillsTex = ContentFinder<Texture2D>.Get("UI/Icons/MainButtons/script-text");
    private static readonly Texture2D WorkerTex = ContentFinder<Texture2D>.Get("UI/Icons/MainButtons/account-details");
    private static readonly Texture2D PowerTex = ContentFinder<Texture2D>.Get("UI/Icons/MainButtons/flash");
    private static readonly Texture2D BatteryTex = ContentFinder<Texture2D>.Get("UI/Icons/MainButtons/battery-40");
    private static readonly Texture2D ValueTex = ContentFinder<Texture2D>.Get("UI/Icons/MainButtons/finance");
    private static readonly Texture2D FertilityTex = PawnReadoutDrawer.FertilityTex;
    private static readonly Texture2D BeautyTex = ContentFinder<Texture2D>.Get("UI/Icons/MainButtons/palette");
    private static readonly Texture2D CoverTex = ContentFinder<Texture2D>.Get("UI/Icons/MainButtons/shield");
    private static readonly Texture2D ResearchTex = ContentFinder<Texture2D>.Get("UI/Icons/MainButtons/flask");
    private static readonly Texture2D ProgressTex = ContentFinder<Texture2D>.Get("UI/Icons/MainButtons/flask-outline");
    private static readonly Texture2D PenetrationTex = ContentFinder<Texture2D>.Get("UI/Icons/MainButtons/needle");
    private static readonly Texture2D HitPointsTex =ContentFinder<Texture2D>.Get("UI/VanillaUIPlus/ArmorDurability");
    private static readonly Texture2D TemperatureTex = ContentFinder<Texture2D>.Get("UI/Commands/TempReset");
    private static readonly Texture2D NutritionTex = ContentFinder<Texture2D>.Get("UI/Icons/MainButtons/silverware-fork-knife");
    private static readonly Texture2D FreshnessTex = ContentFinder<Texture2D>.Get("UI/Icons/MainButtons/clock-time-four");
    private static readonly Texture2D DecayTex = ContentFinder<Texture2D>.Get("UI/Icons/MainButtons/alert-circle");
    private static readonly Texture2D SpaceTex = ContentFinder<Texture2D>.Get("UI/Icons/MainButtons/view-headline");
    private static readonly Texture2D ContentsTex = ContentFinder<Texture2D>.Get("UI/Icons/MainButtons/treasure-chest");
    private static readonly Texture2D PriorityTex = ContentFinder<Texture2D>.Get("UI/Icons/MainButtons/flag-variant");
    private static readonly Texture2D FilterTex = ContentFinder<Texture2D>.Get("UI/Icons/MainButtons/file-tree");

    // Reused while building a view; cleared before each use.
    private static readonly List<string> DeteriorationReasons = new List<string>();
    private static readonly Dictionary<ThingDef, int> ContentCounts = new Dictionary<ThingDef, int>();

    private static View? view;
    // The world pane's view, kept apart so the map and world panes don't rebuild each other's.
    private static View? worldView;
    private static int selectedFrame = -1;
    private static ISelectable? selected;
    // Width of the text area at the last draw, for measuring the leftover text; kept for the session.
    private static float bodyWidth = 408f;

    private sealed class View
    {
        public ISelectable Selectable = null!;
        public float RealTime;
        public float Top;
        public readonly List<ReadoutCard> Cards = new List<ReadoutCard>();
        public string Text = string.Empty;
        public float TextHeight;
        // Inspect lines other mods add that start with a card's label; they move into that card's tooltip.
        public readonly List<PrefixCover> Prefixes = new List<PrefixCover>();
    }

    private sealed class PrefixCover
    {
        public string Prefix = string.Empty;
        public readonly List<string> Lines = new List<string>();

        public string Joined => Lines.Count > 0 ? string.Join("\n", Lines) + "\n\n" : string.Empty;
    }

    /// <summary>The single selected building, item or stockpile when it gets cards, otherwise null; worked out once per frame.</summary>
    public static ISelectable? Selected()
    {
        int frame = Time.frameCount;
        if (frame != selectedFrame)
        {
            selectedFrame = frame;
            selected = FindSelected();
        }

        return selected;
    }

    private static ISelectable? FindSelected()
    {
        if (Find.Selector is not { NumSelected: 1 } selector)
        {
            return null;
        }

        ISelectable? candidate = selector.SingleSelectedThing is Thing thing
            ? Handles(thing) ? thing : null
            : selector.SelectedZone is Zone zone && zone.Map != null && zone.Cells.Count > 0 && HandlesZone(zone) ? zone : null;
        return candidate != null && ViewFor(candidate).Cards.Count > 0 ? candidate : null;
    }

    // Growing zones have their own readout.
    private static bool HandlesZone(Zone zone)
    {
        return zone switch
        {
            Zone_Growing => false,
            Zone_Stockpile => UiPlusMod.Settings.storagePaneEnabled,
            _ => UiPlusMod.Settings.buildingPaneEnabled,
        };
    }

    private static bool Handles(Thing thing)
    {
        if (!thing.Spawned || thing is Pawn || thing.def.hideInspect)
        {
            return false;
        }

        UiPlusSettings settings = UiPlusMod.Settings;
        return thing.def.category switch
        {
            ThingCategory.Building => settings.workbenchPaneEnabled || settings.powerPaneEnabled || settings.storagePaneEnabled || settings.buildingPaneEnabled,
            ThingCategory.Item => settings.itemPaneEnabled,
            ThingCategory.Plant => settings.plantPaneEnabled && thing is Plant,
            _ => false,
        };
    }

    public static float PaneHeight(ISelectable selectable)
    {
        View current = ViewFor(selectable);
        float body = PawnReadout.SectionGap + PawnReadoutDrawer.CardRowsHeight(current.Cards) + current.TextHeight + PawnReadout.SectionGap;
        return Mathf.Max(InspectPaneUtility.PaneHeight, current.Top + body + PawnReadoutDrawer.PaneChromeHeight);
    }

    // Replaces the base game's inspect text: the cards, then the text they don't cover.
    public static void Draw(ISelectable selectable, Rect rect)
    {
        View current = ViewFor(selectable);
        bodyWidth = rect.width;
        try
        {
            float y = PawnReadoutDrawer.DrawCards(rect, rect.y + PawnReadout.SectionGap, current.Cards, 0x5C1C01);
            if (current.Text.Length > 0)
            {
                ReadoutFont.InspectText(current.Text, new Rect(rect.x, y, rect.width, Mathf.Max(0f, rect.yMax - y)));
            }
        }
        catch (Exception exception)
        {
            Log.ErrorOnce($"[Vanilla UI+] Error drawing object readout for {selectable}: {exception}", 0x5C1C7E);
        }
        finally
        {
            GUI.color = Color.white;
            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.UpperLeft;
            Text.WordWrap = true;
        }
    }

    // Drops the cached view so an unloaded game isn't kept in memory.
    public static void Forget()
    {
        view = null;
        worldView = null;
        selected = null;
        selectedFrame = -1;
        worldSelected = null;
        worldFrame = -1;
        ContentCounts.Clear();
    }

    private static View ViewFor(ISelectable selectable)
    {
        float now = Time.realtimeSinceStartup;
        bool world = selectable is WorldObject;
        View? current = world ? worldView : view;
        if (current != null && current.Selectable == selectable && now - current.RealTime < RefreshSeconds && now >= current.RealTime)
        {
            return current;
        }

        current = new View { Selectable = selectable, RealTime = now };
        HashSet<string> covered = new HashSet<string>();
        UiPlusSettings settings = UiPlusMod.Settings;
        if (selectable is Thing thing)
        {
            current.Top = thing.def.onlyShowInspectString ? 0f : ThingTop;
            if (thing.def.category == ThingCategory.Building)
            {
                if (settings.workbenchPaneEnabled && thing is Building_WorkTable table)
                {
                    AddWorkbenchCards(current, table);
                }

                if (settings.workbenchPaneEnabled && thing is Building_ResearchBench bench)
                {
                    AddResearchCards(current, bench, covered);
                }

                if (settings.workbenchPaneEnabled && thing is ThingWithComps facility)
                {
                    AddFacilityCards(current, facility, covered);
                }

                if (settings.powerPaneEnabled && thing is ThingWithComps building)
                {
                    AddPowerCards(current, building, covered);
                    AddTemperatureCards(current, building, covered);
                    AddFuelCard(current, building, covered);
                }

                if (settings.storagePaneEnabled && thing is Building_Storage storage)
                {
                    AddStorageCards(current, storage, storage.GetSlotGroup(), storage.Map, covered);
                }

                if (settings.buildingPaneEnabled)
                {
                    AddOtherBuildingCards(current, thing, covered);
                }

                if (settings.buildingPaneEnabled && current.Cards.Count == 0 && thing is not Frame)
                {
                    AddGeneralBuildingCards(current, thing);
                }

                if (settings.buildingPaneEnabled)
                {
                    AddArtCard(current, thing, covered);
                }
            }
            else if (thing is Plant plant)
            {
                AddPlantCards(current, plant, covered);
            }
            else
            {
                AddItemCards(current, thing, covered);
            }
        }
        else if (selectable is Zone_Stockpile zone)
        {
            AddStorageCards(current, zone, zone.GetSlotGroup(), zone.Map, covered);
            covered.Add($"{"Size".Translate().CapitalizeFirst()}: {zone.CellCount}");
        }
        else if (selectable is Zone otherZone)
        {
            AddZoneCards(current, otherZone, covered);
        }
        else if (selectable is WorldObject worldObject)
        {
            AddWorldCards(current, worldObject, covered);
        }

        if (current.Cards.Count > 0)
        {
            current.Text = LeftoverText(selectable, covered, current.Prefixes);
            if (current.Text.Length > 0)
            {
                current.TextHeight = ReadoutFont.WrappedHeight(current.Text, bodyWidth);
            }
        }

        if (world)
        {
            worldView = current;
        }
        else
        {
            view = current;
        }

        return current;
    }

    // The base game's inspect text (InspectPaneFiller.DrawInspectStringFor) without the lines the cards show.
    private static string LeftoverText(ISelectable selectable, HashSet<string> covered, List<PrefixCover> prefixes)
    {
        string text;
        try
        {
            text = selectable.GetInspectString() ?? string.Empty;
            if (selectable is Thing thing)
            {
                string lowPriority = thing.GetInspectStringLowPriority();
                if (!lowPriority.NullOrEmpty())
                {
                    text = text.NullOrEmpty() ? lowPriority : text.TrimEndNewlines() + "\n" + lowPriority;
                }
            }
        }
        catch (Exception exception)
        {
            Log.ErrorOnce($"[Vanilla UI+] GetInspectString exception on {selectable}: {exception}", 0x5C1C7F);
            return string.Empty;
        }

        string[] lines = text.Split('\n');
        List<string> kept = new List<string>(lines.Length);
        for (int i = 0; i < lines.Length; i++)
        {
            string line = lines[i].TrimEnd('\r');
            if (line.Length == 0)
            {
                continue;
            }

            // Lines another mod has colored are matched without their tags.
            string plain = line.IndexOf('<') >= 0 ? line.StripTags() : line;
            if (!covered.Contains(line) && !covered.Contains(plain) && !MovedToCard(line, prefixes) && (ReferenceEquals(plain, line) || !MovedToCard(plain, prefixes)))
            {
                kept.Add(line);
            }
        }

        return string.Join("\n", kept);
    }

    private static bool MovedToCard(string line, List<PrefixCover> prefixes)
    {
        for (int i = 0; i < prefixes.Count; i++)
        {
            if (line.StartsWith(prefixes[i].Prefix, StringComparison.Ordinal))
            {
                prefixes[i].Lines.Add(line);
                return true;
            }
        }

        return false;
    }

    // Lines starting with "<label>:" are taken out of the text for the card with that label.
    private static PrefixCover CoverLabel(View current, string label)
    {
        PrefixCover cover = new PrefixCover { Prefix = label + ":" };
        current.Prefixes.Add(cover);
        return cover;
    }

    private static void Cover(HashSet<string> covered, string? text)
    {
        if (text.NullOrEmpty())
        {
            return;
        }

        string[] lines = text!.Split('\n');
        for (int i = 0; i < lines.Length; i++)
        {
            covered.Add(lines[i].TrimEnd('\r'));
        }
    }

    private static string Tip(string title, string body)
    {
        return title.AsTipTitle() + "\n\n" + body;
    }

    private static string Hint(string key)
    {
        return "\n\n" + TextCache.Key(key).Colorize(ColoredText.SubtleGrayColor);
    }

    private static string StatExplanation(Thing thing, StatDef stat, float value)
    {
        return stat.Worker.GetExplanationFull(StatRequest.For(thing), stat.toStringNumberSense, value);
    }

    // Bills that can be worked on now, who is working here, and the bench's work speed.
    private static void AddWorkbenchCards(View current, Building_WorkTable table)
    {
        BillStack bills = table.BillStack;
        int active = 0;
        StringBuilder billLines = new StringBuilder();
        for (int i = 0; i < bills.Count; i++)
        {
            Bill bill = bills[i];
            bool now = bill.ShouldDoNow();
            if (now)
            {
                active++;
            }

            string line = bill.LabelCap + (bill is Bill_Production production ? " (" + production.RepeatInfoText + ")" : string.Empty);
            billLines.AppendLine(now ? line : line.Colorize(ColoredText.SubtleGrayColor));
        }

        string billsTitle = "TabBills".Translate().Resolve();
        string billsBody = bills.Count == 0 ? TextCache.Key("VUIP.ObjectPaneNoBills")
            : "VUIP.ObjectPaneBillsTip".Translate(active, bills.Count).Resolve() + "\n\n" + billLines.ToString().TrimEndNewlines();
        current.Cards.Add(new ReadoutCard
        {
            IconTexture = BillsTex, Caption = billsTitle, Value = active + " / " + bills.Count,
            ValueColor = active > 0 ? Color.white : PawnReadoutDrawer.MutedColor,
            Tip = () => Tip(billsTitle, billsBody) + Hint("VUIP.ObjectPaneBillsClick"),
            Click = () => InspectPaneUtility.OpenTab(typeof(ITab_Bills))
        });

        BillStack stack = table.BillStack;
        AddWorkerCard(current, PawnAt(table, pawn => pawn.CurJob.bill?.billStack == stack), "VUIP.ObjectPaneWorkerShort", "VUIP.ObjectPaneNoWorkerTip");

        StatDef speedStat = StatDefOf.WorkTableWorkSpeedFactor;
        float speed = table.GetStatValue(speedStat);
        PrefixCover speedLines = CoverLabel(current, speedStat.LabelCap);
        current.Cards.Add(new ReadoutCard
        {
            Icon = 5, Caption = TextCache.Key("VUIP.PawnPaneWorkSpeedShort"), Value = speed.ToStringPercent(),
            ValueColor = speed < 0.999f ? PawnReadoutDrawer.WarningTextColor : Color.white,
            Tip = () => Tip(speedStat.LabelCap, speedLines.Joined + StatExplanation(table, speedStat, speed))
        });
    }

    // A facility's stat offsets, and the buildings it is linked to.
    private static void AddFacilityCards(View current, ThingWithComps building, HashSet<string> covered)
    {
        CompFacility? facility = building.GetComp<CompFacility>();
        if (facility == null)
        {
            return;
        }

        string text = facility.CompInspectStringExtra() ?? string.Empty;
        Cover(covered, text);
        bool inactive = text.Contains("InactiveFacility".Translate().Resolve());
        List<StatModifier>? offsets = facility.StatOffsets;
        if (offsets != null)
        {
            for (int i = 0; i < offsets.Count; i++)
            {
                StatModifier offset = offsets[i];
                string title = offset.stat.LabelCap.Resolve();
                current.Cards.Add(new ReadoutCard
                {
                    Icon = offset.stat == StatDefOf.WorkTableWorkSpeedFactor ? 5 : -1, IconThing = offset.stat == StatDefOf.WorkTableWorkSpeedFactor ? null : building.def,
                    Caption = title, Value = offset.ValueToStringAsOffset, ValueColor = inactive ? PawnReadoutDrawer.MutedColor : PawnReadoutDrawer.GoodColor,
                    Tip = () => Tip(title, text)
                });
            }
        }

        List<Thing> linked = facility.LinkedBuildings;
        string linkedTitle = TextCache.Key("VUIP.ObjectPaneLinkedShort");
        StringBuilder names = new StringBuilder();
        names.Append(linked.Count == 0 ? TextCache.Key("VUIP.ObjectPaneLinkedNone") : "VUIP.ObjectPaneLinkedTip".Translate(linked.Count).Resolve());
        for (int i = 0; i < linked.Count; i++)
        {
            names.Append("\n" + linked[i].LabelCap);
        }

        if (facility.Props.showMaxSimultaneous)
        {
            names.Append("\n\n" + "FacilityMaxSimultaneousConnections".Translate() + ": " + facility.Props.maxSimultaneous);
        }

        string linkedBody = names.ToString();
        current.Cards.Add(new ReadoutCard
        {
            IconInstance = linked.Count > 0 ? linked[0] : null, IconThing = linked.Count > 0 ? null : building.def, Caption = linkedTitle, Value = linked.Count.ToString(),
            ValueColor = linked.Count > 0 ? Color.white : PawnReadoutDrawer.MutedColor, Tip = () => Tip(linkedTitle, linkedBody)
        });
    }

    // A heater's or cooler's target temperature and power mode.
    private static void AddTemperatureCards(View current, ThingWithComps building, HashSet<string> covered)
    {
        CompTempControl? control = building.GetComp<CompTempControl>();
        if (control == null)
        {
            return;
        }

        string text = control.CompInspectStringExtra() ?? string.Empty;
        Cover(covered, text);
        string targetTitle = TextCache.Key("VUIP.ObjectPaneTargetShort");
        current.Cards.Add(new ReadoutCard
        {
            IconTexture = TemperatureTex, Caption = targetTitle, Value = control.TargetTemperature.ToStringTemperature("F0"), Tip = () => Tip(targetTitle, text)
        });

        if (building.GetComp<CompPowerTrader>() is CompPowerTrader trader)
        {
            string modeTitle = TextCache.Key("VUIP.ObjectPanePowerUseShort");
            string mode = (trader.Off ? "PowerConsumptionOff" : control.operatingAtHighPower ? "PowerConsumptionHigh" : "PowerConsumptionLow").Translate().Resolve().CapitalizeFirst();
            current.Cards.Add(new ReadoutCard
            {
                IconTexture = PowerTex, Caption = modeTitle, Value = mode,
                ValueColor = trader.Off ? PawnReadoutDrawer.MutedColor : control.operatingAtHighPower ? PawnReadoutDrawer.WarningTextColor : Color.white,
                Tip = () => Tip(modeTitle, text)
            });
        }
    }

    // The current project and its progress, who is researching here, and the bench's research speed.
    private static void AddResearchCards(View current, Building_ResearchBench bench, HashSet<string> covered)
    {
        ResearchProjectDef? project = Find.ResearchManager.GetProject();
        string projectTitle = "CurrentProject".Translate().Resolve();
        Action openTab = () => Find.MainTabsRoot.SetCurrentTab(MainButtonDefOf.Research);
        if (project != null)
        {
            // Same lines as Building_ResearchBench.GetInspectString.
            string progressLine = $"{"ResearchProgress".Translate()}: {project.ProgressApparent:F0} / {project.CostApparent:F0} ({project.ProgressPercent.ToStringPercent("0.#")})";
            covered.Add($"{"CurrentProject".Translate()}: {project.LabelCap}");
            covered.Add(progressLine);
            string projectBody = project.LabelCap + "\n" + progressLine;
            current.Cards.Add(new ReadoutCard
            {
                IconTexture = ResearchTex, Caption = projectTitle, Value = project.LabelCap,
                Tip = () => Tip(projectTitle, projectBody) + Hint("VUIP.ObjectPaneResearchClick"), Click = openTab
            });
            string progressTitle = "ResearchProgress".Translate().Resolve();
            current.Cards.Add(new ReadoutCard
            {
                IconTexture = ProgressTex, Caption = progressTitle, Value = project.ProgressPercent.ToStringPercent("0.#"),
                Tip = () => Tip(progressTitle, progressLine), Click = openTab
            });
        }
        else
        {
            covered.Add($"{"CurrentProject".Translate()}: {"None".Translate()}");
            current.Cards.Add(new ReadoutCard
            {
                IconTexture = ResearchTex, Caption = projectTitle, Value = "None".Translate().Resolve(), ValueColor = PawnReadoutDrawer.MutedColor,
                Tip = () => Tip(projectTitle, TextCache.Key("VUIP.ObjectPaneNoResearch")) + Hint("VUIP.ObjectPaneResearchClick"), Click = openTab
            });
        }

        Pawn? researcher = PawnAt(bench, pawn => pawn.CurJobDef == JobDefOf.Research && pawn.CurJob.targetA.Thing == bench);
        AddWorkerCard(current, researcher, "VUIP.ObjectPaneResearcherShort", "VUIP.ObjectPaneNoResearcherTip");

        StatDef speedStat = StatDefOf.ResearchSpeedFactor;
        float speed = bench.GetStatValue(speedStat);
        PrefixCover speedLines = CoverLabel(current, speedStat.LabelCap);
        current.Cards.Add(new ReadoutCard
        {
            Icon = 5, Caption = TextCache.Key("VUIP.ObjectPaneSpeedShort"), Value = speed.ToStringPercent(),
            ValueColor = speed < 0.999f ? PawnReadoutDrawer.WarningTextColor : Color.white,
            Tip = () => Tip(speedStat.LabelCap, speedLines.Joined + StatExplanation(bench, speedStat, speed))
        });
    }

    // Growth, growth rate and harvest; the game's lines for these move into the tooltips, and dying warnings stay as text.
    private static void AddPlantCards(View current, Plant plant, HashSet<string> covered)
    {
        if (!plant.def.plant.showGrowthInInspectPane)
        {
            return;
        }

        // Same text as Plant.GrowthPercentString.
        string growthLine = "PercentGrowth".Translate((Mathf.Floor((plant.Growth + 0.0001f) * 100f) / 100f).ToStringPercent()).Resolve();
        covered.Add(growthLine);
        string growthTitle = TextCache.Key("VUIP.ZonePaneGrowthShort");
        bool growing = plant.LifeStage == PlantLifeStage.Growing;
        current.Cards.Add(new ReadoutCard
        {
            IconTexture = PawnReadoutDrawer.GrowthTex, Caption = growthTitle, Value = plant.Growth.ToStringPercent(),
            ValueColor = plant.HarvestableNow ? PawnReadoutDrawer.GoodColor : Color.white, Tip = () => Tip(growthTitle, growthLine)
        });

        if (growing)
        {
            string rateLabel = "GrowthRate".Translate().Resolve();
            PrefixCover rateLines = CoverLabel(current, rateLabel);
            float rate = plant.GrowthRate;
            string calc = plant.GrowthRateCalcDesc;
            current.Cards.Add(new ReadoutCard
            {
                IconTexture = FertilityTex, Caption = TextCache.Key("VUIP.ObjectPaneRateShort"), Value = rate.ToStringPercent(),
                ValueColor = rate <= 0f ? ColorLibrary.RedReadable : rate < 0.99f ? PawnReadoutDrawer.WarningTextColor : Color.white,
                Tip = () => Tip(rateLabel, rateLines.Joined + calc)
            });
        }
        else if (plant.LifeStage == PlantLifeStage.Mature)
        {
            covered.Add(plant.HarvestableNow ? "ReadyToHarvest".Translate().Resolve() : "Mature".Translate().Resolve());
        }

        ThingDef? product = plant.def.plant.harvestedThingDef;
        if (product != null && plant.def.plant.Harvestable)
        {
            int yield = plant.YieldNow();
            bool ready = plant.HarvestableNow;
            string title = (plant.def.plant.IsTree ? "VUIP.ObjectPaneWoodShort" : "VUIP.ObjectPaneHarvestShort").Translate().Resolve();
            current.Cards.Add(new ReadoutCard
            {
                IconThing = product, Caption = title, Value = ready ? yield + " " + product.label : TextCache.Key("VUIP.ObjectPaneNotReady"),
                ValueColor = ready ? PawnReadoutDrawer.GoodColor : PawnReadoutDrawer.MutedColor,
                Tip = () => Tip(title, ready ? "VUIP.ObjectPaneHarvestTip".Translate(yield, product.label).Resolve()
                    : "VUIP.ObjectPaneNotReadyTip".Translate(plant.def.LabelCap, plant.def.plant.harvestMinGrowth.ToStringPercent()).Resolve())
            });
        }
    }

    // For buildings with no other cards (walls, ancient junk, rock): value, beauty, cover, and what deconstructing or mining gives.
    private static void AddGeneralBuildingCards(View current, Thing thing)
    {
        if (thing.def.building?.mineableThing is ThingDef mined && thing.def.building.mineableYield > 0)
        {
            string title = TextCache.Key("VUIP.ObjectPaneMineShort");
            int yield = thing.def.building.mineableYield;
            current.Cards.Add(new ReadoutCard
            {
                IconThing = mined, Caption = title, Value = yield + " " + mined.label,
                Tip = () => Tip(title, "VUIP.ObjectPaneMineTip".Translate(yield, mined.label).Resolve())
            });
        }
        else
        {
            AddDeconstructCard(current, thing);
        }

        AddQualityCard(current, thing);

        if (thing.def.useHitPoints || thing.MarketValue > 0f)
        {
            AddStatCard(current, thing, StatDefOf.MarketValue, ValueTex, v => v.ToStringMoney(), perStack: false);
        }

        AddStatCard(current, thing, StatDefOf.Beauty, BeautyTex, v => v.ToString("0.#"), perStack: false);
        if (thing.def.fillPercent > 0f)
        {
            string title = TextCache.Key("VUIP.ObjectPaneCoverShort");
            float cover = thing.def.fillPercent;
            current.Cards.Add(new ReadoutCard
            {
                IconTexture = CoverTex, Caption = title, Value = cover.ToStringPercent(),
                Tip = () => Tip(title, "VUIP.ObjectPaneCoverTip".Translate(cover.ToStringPercent()).Resolve())
            });
        }
    }

    // Resources returned on deconstruction, worked out as GenLeaving does (forced leavings in full, blacklisted ones never).
    private static void AddDeconstructCard(View current, Thing thing)
    {
        if (thing is not Building building || !building.DeconstructibleBy(Faction.OfPlayer) || thing.def.resourcesFractionWhenDeconstructed <= 0f)
        {
            return;
        }

        List<ThingDefCountClass> costs = thing.CostListAdjusted();
        BuildingProperties? props = thing.def.building;
        StringBuilder lines = new StringBuilder();
        ThingDef? first = null;
        int firstCount = 0;
        int kinds = 0;
        for (int i = 0; i < costs.Count; i++)
        {
            ThingDef def = costs[i].thingDef;
            if (props?.leavingsBlacklist != null && props.leavingsBlacklist.Contains(def))
            {
                continue;
            }

            int count = props?.forcedCostLeavings != null && props.forcedCostLeavings.Contains(def)
                ? costs[i].count
                : Mathf.Min(Mathf.RoundToInt(costs[i].count * thing.def.resourcesFractionWhenDeconstructed), costs[i].count);
            if (count <= 0)
            {
                continue;
            }

            kinds++;
            if (first == null)
            {
                first = def;
                firstCount = count;
            }

            lines.Append("\n" + count + " " + def.label);
        }

        if (first == null)
        {
            return;
        }

        string title = TextCache.Key("VUIP.ObjectPaneDeconstructShort");
        string body = TextCache.Key("VUIP.ObjectPaneDeconstructTip") + "\n" + lines;
        current.Cards.Add(new ReadoutCard
        {
            IconThing = first, Caption = title, Value = firstCount + " " + first.label + (kinds > 1 ? " +" + (kinds - 1) : string.Empty),
            Tip = () => Tip(title, body)
        });
    }

    // The colony pawn on the building's map that matches, if any.
    private static Pawn? PawnAt(Thing building, Func<Pawn, bool> match)
    {
        List<Pawn> pawns = building.Map.mapPawns.SpawnedPawnsInFaction(Faction.OfPlayer);
        for (int i = 0; i < pawns.Count; i++)
        {
            if (pawns[i].CurJob != null && match(pawns[i]))
            {
                return pawns[i];
            }
        }

        return null;
    }

    private static void AddWorkerCard(View current, Pawn? worker, string captionKey, string noneTipKey)
    {
        string title = TextCache.Key(captionKey);
        if (worker != null)
        {
            current.Cards.Add(new ReadoutCard
            {
                IconInstance = worker, Caption = title, Value = worker.LabelShortCap,
                Tip = () => Tip(worker.LabelCap, worker.jobs?.curDriver?.GetReport()?.CapitalizeFirst() ?? string.Empty) + Hint("VUIP.ObjectPaneWorkerClick"),
                Click = () => CameraJumper.TryJumpAndSelect(worker)
            });
            return;
        }

        current.Cards.Add(new ReadoutCard
        {
            IconTexture = WorkerTex, Caption = title, Value = TextCache.Key("VUIP.ObjectPaneNoWorker"), ValueColor = PawnReadoutDrawer.MutedColor,
            Tip = () => Tip(title, TextCache.Key(noneTipKey))
        });
    }

    // This building's draw or output, its battery, and its grid's surplus.
    private static void AddPowerCards(View current, ThingWithComps building, HashSet<string> covered)
    {
        CompPower? power = building.GetComp<CompPower>();
        if (power == null)
        {
            return;
        }

        List<ThingComp> comps = building.AllComps;
        for (int i = 0; i < comps.Count; i++)
        {
            if (comps[i] is CompPower)
            {
                Cover(covered, comps[i].CompInspectStringExtra());
            }
        }

        if (building.GetComp<CompPowerTrader>() is CompPowerTrader trader)
        {
            string title = TextCache.Key("VUIP.ObjectPanePowerShort");
            float output = trader.PowerOutput;
            string text = trader.CompInspectStringExtra() ?? string.Empty;
            ReadoutCard card = new ReadoutCard { IconTexture = PowerTex, Caption = title, Tip = () => Tip(title, text) };
            if (trader.Off)
            {
                bool unpowered = building.Spawned && !trader.PowerOn && FlickUtility.WantsToBeOn(building) && !building.IsBrokenDown();
                card.Value = TextCache.Key(unpowered ? "VUIP.ObjectPaneUnpowered" : "VUIP.ObjectPanePowerOff");
                card.ValueColor = unpowered ? ColorLibrary.RedReadable : PawnReadoutDrawer.MutedColor;
            }
            else
            {
                card.Value = Watts(output);
                card.ValueColor = output > 0f ? PawnReadoutDrawer.GoodColor : output < 0f ? Color.white : PawnReadoutDrawer.MutedColor;
            }

            current.Cards.Add(card);
        }

        if (building.GetComp<CompPowerBattery>() is CompPowerBattery battery)
        {
            string title = TextCache.Key("VUIP.ObjectPaneBatteryShort");
            float pct = battery.StoredEnergyPct;
            string text = battery.CompInspectStringExtra() ?? string.Empty;
            current.Cards.Add(new ReadoutCard
            {
                IconTexture = BatteryTex, Caption = title, Value = battery.StoredEnergy.ToString("F0") + " / " + battery.Props.storedEnergyMax.ToString("F0") + " Wd",
                ValueColor = pct <= 0f ? ColorLibrary.RedReadable : pct < 0.25f ? PawnReadoutDrawer.WarningTextColor : Color.white,
                Tip = () => Tip(title, text)
            });
        }

        string gridTitle = TextCache.Key("VUIP.ObjectPaneGridShort");
        PowerNet? net = power.PowerNet;
        if (net == null)
        {
            string notConnected = "PowerNotConnected".Translate().Resolve();
            current.Cards.Add(new ReadoutCard
            {
                IconThing = ThingDefOf.PowerConduit, Caption = gridTitle, Value = TextCache.Key("VUIP.ObjectPaneNoGrid"), ValueColor = ColorLibrary.RedReadable,
                Tip = () => Tip(gridTitle, notConnected)
            });
            return;
        }

        float gain = net.CurrentEnergyGainRate() / CompPower.WattsToWattDaysPerTick;
        float stored = net.CurrentStoredEnergy();
        string gridText = "PowerConnectedRateStored".Translate(gain.ToString("F0"), stored.ToString("F0")).Resolve();
        current.Cards.Add(new ReadoutCard
        {
            IconThing = ThingDefOf.PowerConduit, Caption = gridTitle, Value = Watts(gain),
            ValueColor = gain >= 0f ? Color.white : stored > 0f ? PawnReadoutDrawer.WarningTextColor : ColorLibrary.RedReadable,
            Tip = () => Tip(gridTitle, gridText) + Hint("VUIP.ObjectPaneGridTip")
        });
    }

    private static string Watts(float watts)
    {
        return (watts > 0.5f ? "+" : string.Empty) + watts.ToString("F0") + " W";
    }

    // Fuel held against capacity; the tooltip keeps the base game's time left and target level.
    private static void AddFuelCard(View current, ThingWithComps building, HashSet<string> covered)
    {
        CompRefuelable? fuel = building.GetComp<CompRefuelable>();
        if (fuel == null || fuel.Props.fuelIsMortarBarrel && Find.Storyteller.difficulty.classicMortars)
        {
            return;
        }

        string text = fuel.CompInspectStringExtra() ?? string.Empty;
        Cover(covered, text);
        string title = fuel.Props.FuelLabel.CapitalizeFirst();
        ThingDef? fuelDef = fuel.Props.fuelFilter?.AnyAllowedDef;
        current.Cards.Add(new ReadoutCard
        {
            IconThing = fuelDef, IconTexture = fuelDef == null ? PowerTex : null, Caption = title,
            Value = fuel.Fuel.ToStringDecimalIfSmall() + " / " + fuel.Props.fuelCapacity.ToStringDecimalIfSmall(),
            ValueColor = !fuel.HasFuel ? ColorLibrary.RedReadable : fuel.FuelPercentOfMax < 0.25f ? PawnReadoutDrawer.WarningTextColor : Color.white,
            Tip = () => Tip(title, text)
        });
    }

    // Quality or stack size, market value, mass, nutrition, freshness and deterioration.
    private static void AddItemCards(View current, Thing thing, HashSet<string> covered)
    {
        int count = thing.stackCount;
        if (thing.TryGetQuality(out QualityCategory quality))
        {
            string title = "Quality".Translate().Resolve();
            CoverLabel(current, title);
            current.Cards.Add(new ReadoutCard
            {
                IconInstance = thing, Caption = title, Value = quality.GetLabel().CapitalizeFirst(),
                Tip = () => Tip(thing.LabelCap, title + ": " + quality.GetLabel()) + "\n\n" + "ClickForMoreInfo".Translate().Resolve().Colorize(ColoredText.SubtleGrayColor),
                Click = () => Find.WindowStack.Add(new Dialog_InfoCard(thing))
            });
        }
        // The list view leaves out the stack size and hit points: the pane's title and hit points bar already show them.
        else if (thing.def.stackLimit > 1 && !ReadoutFont.ListView)
        {
            string title = TextCache.Key("VUIP.ObjectPaneStackShort");
            int limit = thing.def.stackLimit;
            current.Cards.Add(new ReadoutCard
            {
                IconInstance = thing, Caption = title, Value = count + " / " + limit,
                Tip = () => Tip(thing.LabelCap, "VUIP.ObjectPaneStackTip".Translate(count, limit).Resolve())
                    + "\n\n" + "ClickForMoreInfo".Translate().Resolve().Colorize(ColoredText.SubtleGrayColor),
                Click = () => Find.WindowStack.Add(new Dialog_InfoCard(thing))
            });
        }

        string massLine = StatDefOf.Mass.LabelCap + ": " + (thing.GetStatValue(StatDefOf.Mass) * count).ToStringMass();
        AddStatCard(current, thing, StatDefOf.MarketValue, ValueTex, v => v.ToStringMoney(), massLine);
        if (thing.def.useHitPoints && !ReadoutFont.ListView)
        {
            AddHitPointsCard(current, thing);
        }

        if (thing.def.IsMeleeWeapon)
        {
            // The list view has room for the stats' full names; cards use short captions.
            bool list = ReadoutFont.ListView;
            AddStatCard(current, thing, StatDefOf.MeleeWeapon_AverageDPS, null, v => StatDefOf.MeleeWeapon_AverageDPS.ValueToString(v), icon: 6,
                caption: list ? null : TextCache.Key("VUIP.PawnPaneMeleeDpsShort"), perStack: false);
            StatDef penetration = VUIPDefOf.MeleeWeapon_AverageArmorPenetration;
            AddStatCard(current, thing, penetration, PenetrationTex, v => penetration.ValueToString(v),
                caption: list ? null : TextCache.Key("VUIP.ObjectPanePenetrationShort"), perStack: false);
        }
        else if (thing.def.IsRangedWeapon)
        {
            AddRangedDpsCard(current, thing);
        }

        if (thing is Apparel)
        {
            AddStatCard(current, thing, StatDefOf.ArmorRating_Sharp, null, v => StatDefOf.ArmorRating_Sharp.ValueToString(v), icon: 0,
                caption: TextCache.Key("VUIP.PawnPaneArmorSharpShort"), perStack: false);
            AddStatCard(current, thing, StatDefOf.ArmorRating_Blunt, null, v => StatDefOf.ArmorRating_Blunt.ValueToString(v), icon: 1,
                caption: TextCache.Key("VUIP.PawnPaneArmorBluntShort"), perStack: false);
            AddStatCard(current, thing, StatDefOf.ArmorRating_Heat, null, v => StatDefOf.ArmorRating_Heat.ValueToString(v), icon: 2,
                caption: TextCache.Key("VUIP.PawnPaneArmorHeatShort"), perStack: false);
        }

        if (thing.def.IsNutritionGivingIngestible)
        {
            AddStatCard(current, thing, StatDefOf.Nutrition, NutritionTex, v => v.ToString("0.##"));
        }

        if (thing.TryGetComp<CompRottable>() is CompRottable rot && rot.Active)
        {
            AddFreshnessCard(current, thing, rot, covered);
        }

        DeteriorationReasons.Clear();
        float rate = SteadyEnvironmentEffects.FinalDeteriorationRate(thing, DeteriorationReasons);
        if (DeteriorationReasons.Count > 0)
        {
            // Same line as Thing.GetInspectStringLowPriority.
            string line = $"{"DeterioratingBecauseOf".Translate()}: {DeteriorationReasons.ToCommaList().CapitalizeFirst()} ({"PerDay".Translate(rate.ToStringByStyle(ToStringStyle.FloatMaxTwo))})";
            covered.Add(line);
            string title = "DeterioratingBecauseOf".Translate().Resolve();
            string body = line;
            if (thing.def.useHitPoints && rate > 0f)
            {
                body += "\n\n" + "VUIP.ObjectPaneDecayTip".Translate(((int)(thing.HitPoints / rate * GenDate.TicksPerDay)).ToStringTicksToPeriod()).Resolve();
            }

            current.Cards.Add(new ReadoutCard
            {
                IconTexture = DecayTex, Caption = title, Value = "PerDay".Translate(rate.ToStringByStyle(ToStringStyle.FloatMaxTwo)).Resolve(),
                ValueColor = PawnReadoutDrawer.WarningTextColor, Tip = () => Tip(title, body)
            });
        }
    }

    // A stat for the whole stack (or one item when perStack is off); the tooltip explains one item.
    private static void AddStatCard(View current, Thing thing, StatDef stat, Texture2D? texture, Func<float, string> format, string? extra = null, int icon = -1,
        string? caption = null, bool perStack = true)
    {
        float each = thing.GetStatValue(stat);
        int count = perStack ? thing.stackCount : 1;
        string title = stat.LabelCap.Resolve();
        current.Cards.Add(new ReadoutCard
        {
            IconTexture = texture, Icon = icon, Caption = caption ?? title, Value = format(each * count),
            Tip = () => Tip(title, (count > 1 ? "VUIP.ObjectPaneStackTotal".Translate(count, format(each), format(each * count)).Resolve() + "\n\n" : string.Empty)
                + (extra != null ? extra + "\n\n" : string.Empty) + StatExplanation(thing, stat, each))
        });
    }

    private static void AddHitPointsCard(View current, Thing thing)
    {
        int hp = thing.HitPoints;
        int max = thing.MaxHitPoints;
        string title = TextCache.Key("VUIP.ObjectPaneHitPointsShort");
        current.Cards.Add(new ReadoutCard
        {
            IconTexture = HitPointsTex, Caption = title, Value = hp + " / " + max,
            ValueColor = hp >= max ? Color.white : hp > max / 2 ? PawnReadoutDrawer.WarningTextColor : ColorLibrary.RedReadable,
            Tip = () => Tip(title, "VUIP.ObjectPaneHitPointsTip".Translate(hp, max, ((float)hp / Mathf.Max(1, max)).ToStringPercent()).Resolve())
        });
    }

    // The weapon's own damage per second if every shot hits, worked out as the pawn readout does but without a shooter.
    private static void AddRangedDpsCard(View current, Thing weapon)
    {
        Verb? verb = weapon.TryGetComp<CompEquippable>()?.PrimaryVerb;
        if (verb == null || verb.IsMeleeAttack)
        {
            return;
        }

        float distance = Mathf.Min(verb.verbProps.range, ShootTuning.DistMedium);
        float weaponHit = verb.verbProps.GetHitChanceFactor(weapon, distance);
        float dps = PawnReadout.RangedDps(null, verb, distance, 1f, weaponHit, out string? tip);
        string title = TextCache.Key("VUIP.PawnPaneRangedDpsShort");
        string body = tip ?? TextCache.Key("VUIP.PawnPaneRangedDpsUnknown");
        current.Cards.Add(new ReadoutCard
        {
            Icon = 6, Caption = title, Value = dps >= 0f ? dps.ToString("0.00") : "-", ValueColor = dps >= 0f ? Color.white : PawnReadoutDrawer.MutedColor,
            Tip = () => tip != null ? body : Tip(title, body)
        });
    }

    // Time until it starts to rot at the current temperature, or its rot stage.
    private static void AddFreshnessCard(View current, Thing thing, CompRottable rot, HashSet<string> covered)
    {
        string text = rot.CompInspectStringExtra() ?? string.Empty;
        Cover(covered, text);
        string title = TextCache.Key("VUIP.ObjectPaneFreshnessShort");
        ReadoutCard card = new ReadoutCard { IconTexture = FreshnessTex, Caption = title, Tip = () => Tip(title, text) };
        switch (rot.Stage)
        {
            case RotStage.Rotting:
                card.Value = "RotStateRotting".Translate().Resolve();
                card.ValueColor = ColorLibrary.RedReadable;
                break;
            case RotStage.Dessicated:
                card.Value = "RotStateDessicated".Translate().Resolve();
                card.ValueColor = PawnReadoutDrawer.MutedColor;
                break;
            default:
                // Same rate and thresholds as CompRottable.CompInspectStringExtra.
                float rate = GenTemperature.RotRateAtTemperature(Mathf.RoundToInt(thing.AmbientTemperature));
                if (rate < 0.001f)
                {
                    card.Value = "CurrentlyFrozen".Translate().Resolve().CapitalizeFirst();
                    card.ValueColor = PawnReadoutDrawer.GoodColor;
                }
                else
                {
                    card.Value = rot.TicksUntilRotAtCurrentTemp.ToStringTicksToPeriod();
                    card.ValueColor = rate < 0.999f ? Color.white : PawnReadoutDrawer.WarningTextColor;
                }

                break;
        }

        current.Cards.Add(card);
    }

    // Space used, contents, total value, priority and filter for a stockpile or storage building.
    private static void AddStorageCards(View current, IStoreSettingsParent parent, SlotGroup group, Map map, HashSet<string> covered)
    {
        ContentCounts.Clear();
        int capacity = 0;
        int stacks = 0;
        int items = 0;
        float value = 0f;
        List<IntVec3> cells = group.CellsList;
        for (int i = 0; i < cells.Count; i++)
        {
            capacity += cells[i].GetMaxItemsAllowedInCell(map);
            List<Thing> things = map.thingGrid.ThingsListAt(cells[i]);
            for (int j = 0; j < things.Count; j++)
            {
                Thing thing = things[j];
                if (!thing.def.EverStorable(willMinifyIfPossible: false))
                {
                    continue;
                }

                stacks++;
                items += thing.stackCount;
                value += thing.MarketValue * thing.stackCount;
                ContentCounts.TryGetValue(thing.def, out int held);
                ContentCounts[thing.def] = held + thing.stackCount;
            }
        }

        if (parent is Building_Storage && stacks > 0)
        {
            // Same line as Building_Storage.GetInspectString.
            StringBuilder stores = new StringBuilder();
            stores.Append("StoresThings".Translate());
            stores.Append(": ");
            stores.Append(group.HeldThings.Select(x => x.LabelShortCap).Distinct().ToCommaList());
            stores.Append(".");
            covered.Add(stores.ToString());
        }

        string spaceTitle = TextCache.Key("VUIP.ObjectPaneSpaceShort");
        string spaceBody = "VUIP.ObjectPaneSpaceTip".Translate(stacks, capacity, cells.Count).Resolve();
        current.Cards.Add(new ReadoutCard
        {
            IconTexture = SpaceTex, Caption = spaceTitle, Value = stacks + " / " + capacity,
            ValueColor = stacks >= capacity ? PawnReadoutDrawer.WarningTextColor : Color.white, Tip = () => Tip(spaceTitle, spaceBody)
        });

        string contentsTitle = "Contents".Translate().Resolve();
        string contentsBody = ContentsText(stacks, items);
        ThingDef? most = null;
        int mostCount = 0;
        foreach (KeyValuePair<ThingDef, int> pair in ContentCounts)
        {
            if (pair.Value > mostCount)
            {
                most = pair.Key;
                mostCount = pair.Value;
            }
        }

        current.Cards.Add(new ReadoutCard
        {
            IconThing = most, IconTexture = most == null ? ContentsTex : null, Caption = contentsTitle,
            Value = stacks > 0 ? items.ToStringCached() : TextCache.Key("VUIP.ObjectPaneEmpty"), ValueColor = stacks > 0 ? Color.white : PawnReadoutDrawer.MutedColor,
            Tip = () => Tip(contentsTitle, contentsBody)
        });

        string valueTitle = StatDefOf.MarketValue.LabelCap.Resolve();
        current.Cards.Add(new ReadoutCard
        {
            IconTexture = ValueTex, Caption = valueTitle, Value = value.ToStringMoney(), Tip = () => Tip(valueTitle, TextCache.Key("VUIP.ObjectPaneStorageValueTip"))
        });

        StorageSettings? settings = parent.GetStoreSettings();
        if (settings == null)
        {
            return;
        }

        string priorityTitle = "Priority".Translate().Resolve();
        current.Cards.Add(new ReadoutCard
        {
            IconTexture = PriorityTex, Caption = priorityTitle, Value = settings.Priority.Label().CapitalizeFirst(),
            Tip = () => Tip(priorityTitle, TextCache.Key("VUIP.ObjectPanePriorityTip")) + Hint("VUIP.ObjectPanePriorityClick"),
            Click = () => PriorityMenu(settings)
        });

        string filterTitle = "Filter".Translate().Resolve();
        int allowed = settings.filter.AllowedDefCount;
        int possible = parent.GetParentStoreSettings()?.filter?.AllowedDefCount ?? 0;
        current.Cards.Add(new ReadoutCard
        {
            IconTexture = FilterTex, Caption = filterTitle, Value = possible > 0 ? allowed + " / " + possible : allowed.ToString(),
            Tip = () => Tip(filterTitle, "VUIP.ObjectPaneFilterTip".Translate(allowed, possible).Resolve()) + Hint("VUIP.ObjectPaneFilterClick"),
            Click = () => InspectPaneUtility.OpenTab(typeof(ITab_Storage))
        });
    }

    // The contents tooltip: totals, then the largest amounts by kind.
    private static string ContentsText(int stacks, int items)
    {
        if (stacks == 0)
        {
            return TextCache.Key("VUIP.ObjectPaneEmptyTip");
        }

        StringBuilder sb = new StringBuilder();
        sb.Append("VUIP.ObjectPaneContentsTip".Translate(items, stacks).Resolve());
        sb.AppendLine();
        List<KeyValuePair<ThingDef, int>> sorted = ContentCounts.OrderByDescending(pair => pair.Value).ThenBy(pair => pair.Key.label).ToList();
        for (int i = 0; i < sorted.Count && i < ContentsTipLines; i++)
        {
            sb.AppendLine();
            sb.Append(sorted[i].Value.ToStringCached() + " " + sorted[i].Key.LabelCap);
        }

        if (sorted.Count > ContentsTipLines)
        {
            sb.AppendLine();
            sb.Append("VUIP.ObjectPaneContentsMore".Translate(sorted.Count - ContentsTipLines).Resolve().Colorize(ColoredText.SubtleGrayColor));
        }

        return sb.ToString();
    }

    // The same choices as the storage tab's priority button (ITab_Storage).
    private static void PriorityMenu(StorageSettings settings)
    {
        List<FloatMenuOption> options = new List<FloatMenuOption>();
        foreach (StoragePriority priority in Enum.GetValues(typeof(StoragePriority)))
        {
            if (priority == StoragePriority.Unstored)
            {
                continue;
            }

            StoragePriority chosen = priority;
            options.Add(new FloatMenuOption(chosen.Label().CapitalizeFirst(), () =>
            {
                settings.Priority = chosen;
                view = null;
            }));
        }

        Find.WindowStack.Add(new FloatMenu(options));
    }
}
