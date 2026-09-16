using System.Collections.Generic;
using System.Text;
using UnityEngine;
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
        // Deliberately not gated on UiPlusMod.Enabled: that toggle governs custom HUD
        // drawing only. This alert stands on its own setting so it keeps working when
        // the HUD is left vanilla.
        if (!UiPlusMod.Settings.showTraderPresentAlert)
        {
            return false;
        }

        Rebuild();
        // Culprits make the alert clickable. A caravan trader is jumped to directly; an
        // orbital ship has no Thing, so the comms console used to call it stands in.
        return culprits.Count > 0 ? AlertReport.CulpritsAre(culprits) : AlertReport.Inactive;
    }

    private void Rebuild()
    {
        // The readout asks for a report each frame, and the info pane asks again while it
        // is open, so the scan is done once per frame and reused.
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

            // An orbital ship can only be reached through a comms console, so without one
            // on this map the player has nothing to act on.
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
