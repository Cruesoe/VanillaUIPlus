using System.Collections.Generic;
using System.Text;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace VanillaUIPlus;

/// <summary>
/// Cards in the world map's inspect pane: caravans, settlements and other world objects that belong to a faction.
/// </summary>
public static partial class ObjectReadout
{
    private static readonly Texture2D PeopleTex = ContentFinder<Texture2D>.Get("UI/Icons/MainButtons/account-multiple-plus");
    private static readonly Texture2D FoodTex = ContentFinder<Texture2D>.Get("UI/Icons/MainButtons/silverware-fork-knife");
    private static readonly Texture2D SpeedTex = ContentFinder<Texture2D>.Get("UI/Icons/MainButtons/compass");
    private static readonly Texture2D VisibilityTex = ContentFinder<Texture2D>.Get("UI/Icons/MainButtons/magnify");
    private static readonly Texture2D FoundedTex = ContentFinder<Texture2D>.Get("UI/Icons/MainButtons/calendar-month");
    private static readonly Texture2D WealthTex = ContentFinder<Texture2D>.Get("UI/Icons/MainButtons/bank");
    private static readonly Texture2D RelationTex = ContentFinder<Texture2D>.Get("UI/Icons/MainButtons/flag-variant");

    // The world pane's height without cards (WorldInspectPane uses InspectPaneUtility.PaneSizeFor's fixed height).
    public const float WorldPaneBaseHeight = InspectPaneUtility.PaneHeight;

    private static int worldFrame = -1;
    private static WorldObject? worldSelected;

    /// <summary>The single selected world object when it gets cards, otherwise null; worked out once per frame.</summary>
    public static WorldObject? SelectedWorld()
    {
        int frame = Time.frameCount;
        if (frame != worldFrame)
        {
            worldFrame = frame;
            worldSelected = null;
            if (UiPlusMod.Settings.worldPaneEnabled && Find.World != null && Find.WorldSelector is { NumSelectedObjects: 1 } selector
                && selector.SingleSelectedObject is WorldObject selected && ViewFor(selected).Cards.Count > 0)
            {
                worldSelected = selected;
            }
        }

        return worldSelected;
    }

    /// <summary>How much taller the world pane is while a world object with cards is selected.</summary>
    public static float WorldPaneExtra()
    {
        return SelectedWorld() is WorldObject selected ? Mathf.Max(0f, PaneHeight(selected) - WorldPaneBaseHeight) : 0f;
    }

    private static void AddWorldCards(View current, WorldObject worldObject, HashSet<string> covered)
    {
        switch (worldObject)
        {
            case Caravan caravan:
                AddCaravanCards(current, caravan, covered);
                break;
            case Settlement { Faction: not null } settlement when settlement.Faction.IsPlayer:
                AddColonyCards(current, settlement, covered);
                break;
            default:
                if (worldObject.Faction != null && !worldObject.Faction.IsPlayer)
                {
                    AddFactionCards(current, worldObject, covered);
                }

                break;
        }
    }

    // People, days of food, mass, speed and visibility.
    private static void AddCaravanCards(View current, Caravan caravan, HashSet<string> covered)
    {
        List<Pawn> pawns = caravan.PawnsListForReading;
        int colonists = 0;
        int animals = 0;
        int prisoners = 0;
        int mechs = 0;
        Pawn? firstColonist = null;
        for (int i = 0; i < pawns.Count; i++)
        {
            Pawn pawn = pawns[i];
            if (pawn.IsColonist)
            {
                colonists++;
                firstColonist ??= pawn;
            }
            else if (pawn.RaceProps.Animal)
            {
                animals++;
            }
            else if (pawn.IsPrisoner)
            {
                prisoners++;
            }
            else if (pawn.RaceProps.IsMechanoid)
            {
                mechs++;
            }
        }

        // Same line as Caravan.GetInspectString.
        StringBuilder countLine = new StringBuilder();
        countLine.Append("CaravanColonistsCount".Translate(colonists, colonists == 1 ? Faction.OfPlayer.def.pawnSingular : Faction.OfPlayer.def.pawnsPlural));
        if (animals == 1)
        {
            countLine.Append(", " + "CaravanAnimal".Translate());
        }
        else if (animals > 1)
        {
            countLine.Append(", " + "CaravanAnimalsCount".Translate(animals));
        }

        if (prisoners == 1)
        {
            countLine.Append(", " + "CaravanPrisoner".Translate());
        }
        else if (prisoners > 1)
        {
            countLine.Append(", " + "CaravanPrisonersCount".Translate(prisoners));
        }

        if (ModsConfig.BiotechActive)
        {
            if (mechs == 1)
            {
                countLine.Append(", " + "CaravanMech".Translate());
            }
            else if (mechs > 1)
            {
                countLine.Append(", " + "CaravanMechsCount".Translate(mechs));
            }
        }

        string peopleText = countLine.ToString();
        covered.Add(peopleText);
        string peopleTitle = TextCache.Key("VUIP.WorldPanePeopleShort");
        current.Cards.Add(new ReadoutCard
        {
            IconInstance = firstColonist, IconTexture = firstColonist == null ? PeopleTex : null, Caption = peopleTitle, Value = pawns.Count.ToString(),
            Tip = () => Tip(peopleTitle, peopleText)
        });

        string foodTitle = TextCache.Key("VUIP.WorldPaneFoodShort");
        float days = DaysWorthOfFoodCalculator.ApproxDaysWorthOfFood(caravan);
        bool plenty = days >= 600f;
        current.Cards.Add(new ReadoutCard
        {
            IconTexture = FoodTex, Caption = foodTitle, Value = plenty ? TextCache.Key("VUIP.WorldPaneFoodPlenty") : "VUIP.WorldPaneDays".Translate(days.ToString("0.#")).Resolve(),
            ValueColor = plenty ? Color.white : days < 1f ? ColorLibrary.RedReadable : days < 3f ? PawnReadoutDrawer.WarningTextColor : Color.white,
            Tip = () => Tip(foodTitle, TextCache.Key("VUIP.WorldPaneFoodTip"))
        });

        string massTitle = "Mass".Translate().Resolve();
        float used = caravan.MassUsage;
        float capacity = caravan.MassCapacity;
        current.Cards.Add(new ReadoutCard
        {
            IconTexture = MassTex, Caption = massTitle, Value = used.ToString("F0") + " / " + capacity.ToString("F0") + " kg",
            ValueColor = used > capacity ? ColorLibrary.RedReadable : Color.white,
            Tip = () => Tip(massTitle, "VUIP.ObjectPaneTransporterMassTip".Translate(used.ToString("F0"), capacity.ToString("F0")).Resolve())
        });

        string speedTitle = TextCache.Key("VUIP.WorldPaneSpeedShort");
        StringBuilder speedExplanation = new StringBuilder();
        float tiles = TilesPerDayCalculator.ApproxTilesPerDay(caravan, speedExplanation);
        string speedBody = speedExplanation.ToString().TrimEndNewlines();
        current.Cards.Add(new ReadoutCard
        {
            IconTexture = SpeedTex, Caption = speedTitle, Value = "VUIP.WorldPaneTilesPerDay".Translate(tiles.ToString("0.#")).Resolve(),
            ValueColor = tiles <= 0f ? ColorLibrary.RedReadable : Color.white, Tip = () => Tip(speedTitle, speedBody)
        });

        string visibilityTitle = TextCache.Key("VUIP.WorldPaneVisibilityShort");
        StringBuilder visibilityExplanation = new StringBuilder();
        float visibility = CaravanVisibilityCalculator.Visibility(caravan, visibilityExplanation);
        string visibilityBody = visibilityExplanation.ToString().TrimEndNewlines();
        current.Cards.Add(new ReadoutCard
        {
            IconTexture = VisibilityTex, Caption = visibilityTitle, Value = visibility.ToStringPercent(), Tip = () => Tip(visibilityTitle, visibilityBody)
        });
    }

    // The colony's people, wealth and founding date.
    private static void AddColonyCards(View current, Settlement settlement, HashSet<string> covered)
    {
        PrefixCover settled = CoverLabel(current, "Settled".Translate());
        if (settlement.HasMap)
        {
            Map map = settlement.Map;
            string peopleTitle = TextCache.Key("VUIP.WorldPaneColonistsShort");
            int colonists = map.mapPawns.FreeColonistsSpawnedCount;
            current.Cards.Add(new ReadoutCard
            {
                IconTexture = PeopleTex, Caption = peopleTitle, Value = colonists.ToString(), Tip = () => Tip(peopleTitle, "VUIP.WorldPaneColonistsTip".Translate(colonists).Resolve())
            });

            string wealthTitle = TextCache.Key("VUIP.WorldPaneWealthShort");
            float wealth = map.wealthWatcher.WealthTotal;
            current.Cards.Add(new ReadoutCard
            {
                IconTexture = WealthTex, Caption = wealthTitle, Value = wealth.ToStringMoney("F0"), Tip = () => Tip(wealthTitle, TextCache.Key("VUIP.WorldPaneWealthTip"))
            });
        }

        string foundedTitle = TextCache.Key("VUIP.WorldPaneFoundedShort");
        int ticksAgo = Find.TickManager.TicksGame - settlement.creationGameTicks;
        current.Cards.Add(new ReadoutCard
        {
            IconTexture = FoundedTex, Caption = foundedTitle, Value = ticksAgo.ToStringTicksToPeriodVague(),
            Tip = () => Tip(foundedTitle, settled.Joined.TrimEndNewlines())
        });
    }

    // Who owns it and how they feel about the colony.
    private static void AddFactionCards(View current, WorldObject worldObject, HashSet<string> covered)
    {
        Faction faction = worldObject.Faction;
        CoverLabel(current, "Faction".Translate());
        string factionTitle = "Faction".Translate().Resolve();
        string factionName = faction.Name;
        current.Cards.Add(new ReadoutCard
        {
            IconTexture = faction.def.FactionIcon, Caption = factionTitle, Value = factionName, Rows = 2,
            Tip = () => Tip(factionTitle, factionName + "\n" + faction.def.LabelCap) + "\n\n" + "ClickForMoreInfo".Translate().Resolve().Colorize(ColoredText.SubtleGrayColor),
            Click = () => Find.WindowStack.Add(new Dialog_InfoCard(faction))
        });

        FactionRelationKind kind = faction.PlayerRelationKind;
        string relationTitle = TextCache.Key("VUIP.WorldPaneRelationShort");
        // Same line as Settlement.GetInspectString.
        string relationValue = kind.GetLabelCap() + (faction.Hidden ? string.Empty : " (" + faction.PlayerGoodwill.ToStringWithSign() + ")");
        covered.Add(relationValue);
        current.Cards.Add(new ReadoutCard
        {
            IconTexture = RelationTex, Caption = relationTitle, Value = relationValue,
            ValueColor = kind == FactionRelationKind.Hostile ? ColorLibrary.RedReadable : kind == FactionRelationKind.Ally ? PawnReadoutDrawer.GoodColor : Color.white,
            Tip = () => Tip(relationTitle, relationValue)
        });
    }
}
