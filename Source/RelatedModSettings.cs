using System;
using System.Collections.Generic;
using RimWorld;
using Verse;

namespace VanillaUIPlus;

/// <summary>
/// Shortcuts to the settings of other mods that sit alongside this one, so they can be
/// opened without backing out to the mod list. Only mods that are actually loaded and
/// that actually expose a settings page are listed, so the block disappears entirely for
/// anyone running none of them.
/// </summary>
public static class RelatedModSettings
{
    // RimWorld lowercases package ids as it loads, so these are matched case-insensitively.
    private static readonly string[] PackageIds =
    {
        "mlie.blockunwantedminutiae",
    };

    private static List<Mod>? installed;

    public static bool Any => Installed().Count > 0;

    public static void Draw(Listing_Standard list)
    {
        List<Mod> mods = Installed();
        for (int i = 0; i < mods.Count; i++)
        {
            Mod mod = mods[i];
            if (list.ButtonText("VUIP.OpenModSettings".Translate(mod.SettingsCategory())))
            {
                Find.WindowStack.Add(new Dialog_ModSettings(mod));
            }
        }
    }

    private static List<Mod> Installed()
    {
        if (installed != null)
        {
            return installed;
        }

        // The loaded mod list cannot change without a restart, so this is resolved once.
        installed = new List<Mod>();
        foreach (Mod mod in LoadedModManager.ModHandles)
        {
            if (mod?.Content == null || !Matches(mod.Content.PackageId))
            {
                continue;
            }

            // A mod with no settings category has no page to open.
            if (!mod.SettingsCategory().NullOrEmpty())
            {
                installed.Add(mod);
            }
        }

        return installed;
    }

    private static bool Matches(string packageId)
    {
        for (int i = 0; i < PackageIds.Length; i++)
        {
            if (string.Equals(packageId, PackageIds[i], StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}
