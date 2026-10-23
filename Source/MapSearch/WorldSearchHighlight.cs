using System.Collections.Generic;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace VanillaUIPlus;

/// <summary>The tiles of the open world search's results, for the highlight layer.</summary>
public static class WorldSearchHighlight
{
    private static Dialog_WorldSearch? dialog;
    private static int resultCount = -1;
    private static readonly List<PlanetTile> tiles = new List<PlanetTile>();

    public static int Version { get; private set; }

    public static List<PlanetTile> Tiles => tiles;

    public static bool Showing => UiPlusMod.Settings.worldSearchHighlight && dialog != null && tiles.Count > 0 && Find.WindowStack.IsOpen(dialog);

    public static void Update(Dialog_WorldSearch current, SortedList<string, WorldSearchElement> results)
    {
        if (current == dialog && results.Count == resultCount)
        {
            return;
        }

        dialog = current;
        resultCount = results.Count;
        tiles.Clear();
        foreach (WorldSearchElement element in results.Values)
        {
            if (element != null && element.tile.Valid)
            {
                tiles.Add(element.tile);
            }
        }

        Version++;
    }

    public static void Forget()
    {
        dialog = null;
        resultCount = -1;
        tiles.Clear();
        Version++;
    }

    // A restarted search can refill to the same count before the next draw, so the next update always rebuilds.
    public static void NotifySearchRestarted()
    {
        resultCount = -1;
    }
}
