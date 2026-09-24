using System.Collections.Generic;
using System.Text;
using UnityEngine;
using RimWorld;
using Verse;

namespace VanillaUIPlus;

/// <summary>Stays up while a trade caravan, or an orbital trader reachable by comms console, can be traded with.</summary>
public class Alert_TraderPresent : Alert
{
    private readonly List<Thing> traderPawns = new List<Thing>();
    private readonly List<TradeShip> tradeShips = new List<TradeShip>();
    private readonly List<Thing> consoles = new List<Thing>();
    private readonly List<Thing> culprits = new List<Thing>();
    private readonly StringBuilder explanation = new StringBuilder();
    private int scannedFrame = -1;

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
            explanation.AppendLine("VUIP.TraderPresentOrbital".Translate(ship.FullTitle));
        }

        return "VUIP.TraderPresentDesc".Translate(explanation.ToString().TrimEndNewlines());
    }

    public override AlertReport GetReport()
    {
        // Answers only to its own setting, not the custom HUD toggle.
        if (!UiPlusMod.Settings.showTraderPresentAlert)
        {
            return false;
        }

        Rebuild();
        // Clicking jumps to a caravan trader, or to the comms console for an orbital ship.
        return culprits.Count > 0 ? AlertReport.CulpritsAre(culprits) : AlertReport.Inactive;
    }

    private void Rebuild()
    {
        // Scans at most once per frame.
        if (scannedFrame == Time.frameCount)
        {
            return;
        }

        scannedFrame = Time.frameCount;
        traderPawns.Clear();
        tradeShips.Clear();
        consoles.Clear();
        culprits.Clear();
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

            // Orbital ships count only on maps with a usable comms console.
            Building_CommsConsole? console = UsableCommsConsole(map);
            if (console == null)
            {
                continue;
            }

            int shipsBefore = tradeShips.Count;
            List<PassingShip> ships = map.passingShipManager.passingShips;
            for (int s = 0; s < ships.Count; s++)
            {
                if (ships[s] is TradeShip ship && ship.CanTradeNow)
                {
                    tradeShips.Add(ship);
                }
            }

            if (tradeShips.Count > shipsBefore)
            {
                consoles.Add(console);
            }
        }

        culprits.AddRange(traderPawns);
        culprits.AddRange(consoles);
    }

    private static Building_CommsConsole? UsableCommsConsole(Map map)
    {
        List<Building> buildings = map.listerBuildings.allBuildingsColonist;
        for (int i = 0; i < buildings.Count; i++)
        {
            if (buildings[i] is Building_CommsConsole console && console.CanUseCommsNow)
            {
                return console;
            }
        }

        return null;
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
