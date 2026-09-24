using System;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using Verse;

namespace VanillaUIPlus;

/// <summary>
/// Resolves private game members once; a missing or reshaped member logs one warning and returns null so the caller falls back to vanilla.
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

    /// <summary>A direct reference to an instance field.</summary>
    public static AccessTools.FieldRef<T, F>? FieldRef<T, F>(string owner, string member, FieldInfo? field)
    {
        if (!Found(owner, member, field))
        {
            return null;
        }

        return Bind(owner, member, () => AccessTools.FieldRefAccess<T, F>(field!));
    }

    /// <summary>A direct reference to a static field.</summary>
    public static AccessTools.FieldRef<F>? StaticFieldRef<F>(string owner, string member, FieldInfo? field)
    {
        if (!Found(owner, member, field))
        {
            return null;
        }

        return Bind(owner, member, () => AccessTools.StaticFieldRefAccess<F>(field!));
    }

    /// <summary>A typed delegate for a method.</summary>
    public static TDelegate? Delegate<TDelegate>(string owner, string member, MethodInfo? method)
        where TDelegate : System.Delegate
    {
        if (!Found(owner, member, method))
        {
            return null;
        }

        return Bind(owner, member, () => AccessTools.MethodDelegate<TDelegate>(method!));
    }

    /// <summary>A delegate for a parameterless instance method on a type that can't be named at compile time.</summary>
    public static Func<object, TResult>? UntypedDelegate<TResult>(string owner, string member, MethodInfo? method)
    {
        if (!Found(owner, member, method))
        {
            return null;
        }

        return Bind(owner, member, () =>
        {
            Type declaring = method!.DeclaringType!;
            if (method.IsStatic || declaring.IsValueType || method.GetParameters().Length != 0 || method.ReturnType != typeof(TResult))
            {
                throw new ArgumentException($"Expected an instance method with no parameters returning {typeof(TResult).Name}.");
            }

            DynamicMethod dynamic = new DynamicMethod(
                $"VUIP_{owner}_{member}", typeof(TResult), new[] { typeof(object) }, typeof(ReflectionGuard).Module, skipVisibility: true);
            ILGenerator il = dynamic.GetILGenerator();
            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Castclass, declaring);
            il.Emit(method.IsVirtual ? OpCodes.Callvirt : OpCodes.Call, method);
            il.Emit(OpCodes.Ret);
            return (Func<object, TResult>)dynamic.CreateDelegate(typeof(Func<object, TResult>));
        });
    }

    // Binding throws when a member still exists but has changed shape; that is turned into the null callers already handle.
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
