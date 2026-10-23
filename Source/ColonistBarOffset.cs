using UnityEngine;
using Verse;

namespace VanillaUIPlus;

/// <summary>
/// Moves the colonist bar below the dev-mode toolbar while dev mode is on, by shifting its calculated positions so clicks still line up.
/// </summary>
public static class ColonistBarOffset
{
    private static float appliedOffset = float.NaN;

    public static float CurrentOffset
    {
        get
        {
            if (!Prefs.DevMode || !UiPlusMod.Settings.shiftColonistBarInDevMode)
            {
                return 0f;
            }

            return Mathf.Max(0f, UiPlusMod.Settings.colonistBarDevOffset);
        }
    }

    // Marks the bar's cached positions dirty when the offset changes, such as on toggling dev mode.
    public static void RefreshIfChanged()
    {
        float offset = CurrentOffset;
        if (offset == appliedOffset)
        {
            return;
        }

        appliedOffset = offset;
        Find.ColonistBar?.MarkColonistsDirty();
    }
}
