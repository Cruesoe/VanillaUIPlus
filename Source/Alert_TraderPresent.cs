using System.Collections.Generic;
using System.Text;
using RimWorld;
using Verse;

namespace VanillaUIPlus;

/// <summary>
/// Vanilla only announces a trader with a letter. Once that letter is dismissed or
/// scrolls away nothing says the trader is still standing on the map, so it is easy to
/// forget about them until they leave. This alert stays up for as long as there is
/// actually someone to trade with, covering both caravan traders and orbital ships.
/// </summary>
public class Alert_TraderPresent : Alert
{
    private readonly List<Thing> traderPawns = new List<Thing>();
    private readonly List<TradeShip> tradeShips = new List<TradeShip>();
    private readonly StringBuilder explanation = new StringBuilder();

    public Alert_TraderPresent()
    {
        defaultLabel = "VUIP.TraderPresent".Translate();
        defaultPriority = AlertPriority.Medium;
    }

    public override TaggedString GetExplanation()
    {
        explanation.Length = 0;
        foreach (Thing thing in traderPawns)
        {
            explanation.Append("  - ");
            explanation.AppendLine(DescribeTrader(thing));
        }

        foreach (TradeShip ship in tradeShips)
        {
            explanation.Append("  - ");
            explanation.AppendLine(ship.FullTitle);
        }

        return "VUIP.TraderPresentDesc".Translate(explanation.ToString().TrimEndNewlines());
    }

    public override AlertReport GetReport()
    {
        if (!UiPlusMod.Enabled || !UiPlusMod.Settings.showTraderPresentAlert)
        {
            return false;
        }

        Rebuild();
        if (traderPawns.Count > 0)
        {
            // Culprits make the alert clickable, jumping the camera to the trader.
            return AlertReport.CulpritsAre(traderPawns);
        }

        // An orbital ship has no Thing to jump to, so it can only flag the alert active.
        return tradeShips.Count > 0 ? AlertReport.Active : AlertReport.Inactive;
    }

    private void Rebuild()
    {
        traderPawns.Clear();
        tradeShips.Clear();
        List<Map> maps = Find.Maps;
        for (int i = 0; i < maps.Count; i++)
        {
            Map map = maps[i];
            if (!MapHasPlayerPresence(map))
            {
                continue;
            }

            IReadOnlyList<Pawn> pawns = map.mapPawns.AllPawnsSpawned;
            for (int p = 0; p < pawns.Count; p++)
            {
                Pawn pawn = pawns[p];
                if (pawn.TraderKind == null || pawn.Dead || pawn.Downed)
                {
                    continue;
                }

                if (pawn.HostileTo(Faction.OfPlayer) || !pawn.CanTradeNow)
                {
                    continue;
                }

                traderPawns.Add(pawn);
            }

            if (map.passingShipManager?.passingShips == null)
            {
                continue;
            }

            List<PassingShip> ships = map.passingShipManager.passingShips;
            for (int s = 0; s < ships.Count; s++)
            {
                if (ships[s] is TradeShip ship && ship.CanTradeNow)
                {
                    tradeShips.Add(ship);
                }
            }
        }
    }

    private static string DescribeTrader(Thing thing)
    {
        if (thing is Pawn pawn && pawn.TraderKind != null)
        {
            return pawn.LabelShortCap + " (" + pawn.TraderKind.LabelCap + ")";
        }

        return thing.LabelCap;
    }

    private static bool MapHasPlayerPresence(Map map)
    {
        if (map.IsPlayerHome)
        {
            return true;
        }

        return map.mapPawns.FreeColonistsSpawnedCount > 0;
    }
}
