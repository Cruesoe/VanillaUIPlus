using RimWorld;
using UnityEngine;
using Verse;

namespace VanillaUIPlus;

/// <summary>
/// Redraws the inspect pane's name centered on the header's icon row while the pawn readout's header icons are shown.
/// The base game draws it top-anchored in a 50-high rect (InspectPaneUtility.InspectPaneOnGUI), which puts it below the icons.
/// </summary>
public static class PawnPaneName
{
    // Set only while InspectPaneUtility.InspectPaneOnGUI runs for the main inspect pane; the label is held for that frame only.
    private static bool inPaneOnGUI;
    private static string? label;
    private static Rect labelRect;

    public static void Begin(IInspectPane pane)
    {
        inPaneOnGUI = pane is MainTabWindow_Inspect && (PawnReadout.CardPawn() != null || PawnReadout.SelectedPawn() != null);
        label = null;
    }

    // Takes the label the base game is about to draw, so it can be drawn on the icon row instead.
    public static bool Capture(string result, Rect rect)
    {
        if (!inPaneOnGUI || result.NullOrEmpty())
        {
            return false;
        }

        label = result;
        labelRect = rect;
        return true;
    }

    // Draws the held label in the base game's font, centered on the 24-high icon row like the header's age.
    public static void End(Rect inRect)
    {
        if (inPaneOnGUI && label != null)
        {
            // Same group origin as InspectPaneOnGUI: inRect contracted by 12, top raised by 4.
            Rect rect = new Rect(inRect.x + 12f + labelRect.x, inRect.y + 8f - 3f, labelRect.width + 300f, 30f);
            using (new TextBlock(GameFont.Medium, TextAnchor.MiddleLeft, false))
            {
                Widgets.Label(rect, label);
            }
        }

        inPaneOnGUI = false;
        label = null;
    }
}
