using System;
using System.Reflection;
using HarmonyLib;
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

    /// <summary>
    /// A direct reference to an instance field, for members read or written every frame.
    /// Binding throws rather than returning null when a field still exists but has
    /// changed type, which inside a prefix would mean an exception every frame, so the
    /// failure is turned back into the null the callers already handle.
    /// </summary>
    public static AccessTools.FieldRef<T, F>? FieldRef<T, F>(string owner, string member, FieldInfo? field)
    {
        if (!Found(owner, member, field))
        {
            return null;
        }

        return Bind(owner, member, () => AccessTools.FieldRefAccess<T, F>(field!));
    }

    /// <summary>
    /// As <see cref="FieldRef{T, F}"/>, for a static field.
    /// </summary>
    public static AccessTools.FieldRef<F>? StaticFieldRef<F>(string owner, string member, FieldInfo? field)
    {
        if (!Found(owner, member, field))
        {
            return null;
        }

        return Bind(owner, member, () => AccessTools.StaticFieldRefAccess<F>(field!));
    }

    /// <summary>
    /// As <see cref="FieldRef{T, F}"/>, for a method called every frame.
    /// </summary>
    public static TDelegate? Delegate<TDelegate>(string owner, string member, MethodInfo? method)
        where TDelegate : System.Delegate
    {
        if (!Found(owner, member, method))
        {
            return null;
        }

        return Bind(owner, member, () => AccessTools.MethodDelegate<TDelegate>(method!));
    }

    private static T? Bind<T>(string owner, string member, Func<T> bind)
        where T : class
    {
        try
        {
            return bind();
        }
        catch (Exception exception)
        {
            Log.Warning(
                $"[Vanilla UI+] Could not bind to {owner}.{member}; its shape has changed. "
                + $"Falling back to vanilla drawing for this element; the rest of the mod is unaffected.\n{exception}");
            return null;
        }
    }
}
