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

    // The labels are rebuilt only when a counter changes, about once a second.
    private static int labelFps = int.MinValue;
    private static int labelTps = int.MinValue;
    private static int labelTarget = int.MinValue;
    private static string fpsLabel = string.Empty;
    private static string tpsLabel = string.Empty;

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
        int fps = FramesPerSecond!();
        int tps = TicksActual!();
        int target = TicksTarget!();
        if (fps != labelFps)
        {
            labelFps = fps;
            fpsLabel = $"FPS: {fps}";
        }

        if (tps != labelTps || target != labelTarget)
        {
            labelTps = tps;
            labelTarget = target;
            tpsLabel = $"TPS: {tps}({target})";
        }

        Text.Font = GameFont.Small;
        float lineHeight = Text.LineHeight;
        Rect bar = new Rect(UI.screenWidth - AlertDrawer.BarWidth, curBaseY - lineHeight, AlertDrawer.BarWidth, lineHeight);
        ReadoutDrawer.DrawSplitBar(bar, fpsLabel, tpsLabel);
        curBaseY -= lineHeight;
    }
}

[HarmonyPatch]
public static class Patch_FPSAndTPS_TPSCounter
{
    public static bool Prepare()
    {
        return AccessTools.TypeByName("FPSAndTPS.TPSCounter") != null && FpsAndTpsDisplay.Bound;
    }

    public static MethodBase TargetMethod()
    {
        return AccessTools.Method("FPSAndTPS.TPSCounter:Draw");
    }

    public static bool Prefix()
    {
        return !UiPlusMod.Enabled;
    }
}
