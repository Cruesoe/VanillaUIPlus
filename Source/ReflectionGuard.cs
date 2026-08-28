using Verse;

namespace VanillaUIPlus;

/// <summary>
/// Vanilla UI+ replaces several core draw methods outright via Harmony prefixes that
/// return false. Those replacements reach into private RimWorld members by reflection,
/// so a renamed field in a future game patch would otherwise throw every frame from
/// inside a prefix, leaving no HUD at all and flooding the log.
///
/// Each such patch resolves its members once and asks <see cref="Found"/> whether they
/// are all present. When any is missing the patch steps aside and lets vanilla draw.
/// </summary>
public static class ReflectionGuard
{
    public static bool Found(string owner, string member, object? resolved)
    {
        if (resolved != null)
        {
            return true;
        }

        Log.Warning(
            $"[Vanilla UI+] Could not find {owner}.{member}. RimWorld may have changed. "
            + "Falling back to vanilla drawing for this element; the rest of the mod is unaffected.");
        return false;
    }
}
