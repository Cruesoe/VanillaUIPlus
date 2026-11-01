using System;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using Verse;

namespace VanillaUIPlus;

// Draws the FPS And TPS mod's counter as a bar above the speed controls.
public static class FpsAndTpsDisplay
{
    private static readonly Action? Update;
    private static readonly Func<int>? FramesPerSecond;
    private static readonly Func<int>? TicksActual;
    private static readonly Func<int>? TicksTarget;

    private static readonly CounterBarLabels Labels = new CounterBarLabels();

    public static bool Bound { get; }

    static FpsAndTpsDisplay()
    {
        Type? updater = AccessTools.TypeByName("FPSAndTPS.TickAndFrameUpdater");
        if (updater == null)
        {
            return;
        }

        // A binding failure leaves the mod drawing its own counter.
        Update = ReflectionGuard.Delegate<Action>("TickAndFrameUpdater", "Update", AccessTools.Method(updater, "Update"));
        FramesPerSecond = Getter(updater, "FramesPerSecond");
        TicksActual = Getter(updater, "TicksPerSecondActual");
        TicksTarget = Getter(updater, "TicksPerSecondTarget");
        Bound = Update != null && FramesPerSecond != null && TicksActual != null && TicksTarget != null;
    }

    private static Func<int>? Getter(Type type, string name)
    {
        MethodInfo? getter = AccessTools.PropertyGetter(type, name);
        return ReflectionGuard.Delegate<Func<int>>(type.Name, name, getter);
    }

    public static void Draw(ref float curBaseY)
    {
        if (!Bound)
        {
            return;
        }

        Update!();
        Labels.Draw(FramesPerSecond!(), TicksActual!(), TicksTarget!(), ref curBaseY);
    }
}
