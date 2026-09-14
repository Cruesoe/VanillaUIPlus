using System;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using Verse;

namespace VanillaUIPlus;

public static class DubsTpsDisplay
{
    private static readonly Type? TpsType;
    private static readonly MethodInfo? PrefixMethod;

    // These counters are read and written on every GUI pass, so they are resolved once
    // into direct static field references. FieldInfo.GetValue/SetValue boxed every int
    // and DateTime that crossed this boundary, several times a frame.
    private static readonly AccessTools.FieldRef<bool>? Disable;
    private static readonly AccessTools.FieldRef<DateTime>? PrevTime;
    private static readonly AccessTools.FieldRef<int>? PrevTicks;
    private static readonly AccessTools.FieldRef<int>? TpsActual;
    private static readonly AccessTools.FieldRef<int>? TpsTarget;
    private static readonly AccessTools.FieldRef<int>? PrevFrames;
    private static readonly AccessTools.FieldRef<int>? FpsActual;
    private static readonly Func<bool>? CurrentlyProfiling;
    private static readonly bool CanUpdate;

    static DubsTpsDisplay()
    {
        TpsType = AccessTools.TypeByName("Analyzer.GUIElement_TPS");
        if (TpsType == null)
        {
            return;
        }

        PrefixMethod = AccessTools.Method(TpsType, "Prefix", new[] { typeof(float), typeof(float), typeof(float).MakeByRefType() });

        // Another mod's internals: a field that has changed shape rather than name would
        // throw while being bound, so binding failures fall back to the vanilla drawing
        // path exactly as a missing field already did.
        try
        {
            Type? settingsType = AccessTools.TypeByName("Analyzer.Settings");
            Disable = StaticRef<bool>(settingsType, "disableTPSCounter");

            Type? analyzerType = AccessTools.TypeByName("Analyzer.Profiling.Analyzer");
            PropertyInfo? profilingProp = analyzerType == null ? null : AccessTools.Property(analyzerType, "CurrentlyProfiling");
            MethodInfo? profilingGetter = profilingProp?.GetGetMethod(nonPublic: true);
            CurrentlyProfiling = ReflectionGuard.Delegate<Func<bool>>("Analyzer", "CurrentlyProfiling", profilingGetter);

            PrevTime = StaticRef<DateTime>(TpsType, "prevTime");
            PrevTicks = StaticRef<int>(TpsType, "prevTicks");
            TpsActual = StaticRef<int>(TpsType, "tpsActual");
            TpsTarget = StaticRef<int>(TpsType, "tpsTarget");
            PrevFrames = StaticRef<int>(TpsType, "prevFrames");
            FpsActual = StaticRef<int>(TpsType, "fpsActual");
        }
        catch (Exception exception)
        {
            Log.Warning($"[Vanilla UI+] Could not bind to Dubs Performance Analyzer's TPS counter; drawing it the way that mod does instead.\n{exception}");
        }

        CanUpdate = PrevTime != null
            && PrevTicks != null
            && TpsActual != null
            && TpsTarget != null
            && PrevFrames != null
            && FpsActual != null;
    }

    private static AccessTools.FieldRef<T>? StaticRef<T>(Type? type, string name)
    {
        if (type == null)
        {
            return null;
        }

        return ReflectionGuard.StaticFieldRef<T>(type.Name, name, AccessTools.Field(type, name));
    }

    public static void Draw(ref float curBaseY)
    {
        if (TpsType == null)
        {
            return;
        }

        bool hideCounter = Disable != null && Disable();
        bool profiling = CurrentlyProfiling != null && CurrentlyProfiling();
        if (hideCounter && !profiling)
        {
            return;
        }

        if (!CanUpdate || !TryUpdateCounters())
        {
            DrawVanillaStacked(ref curBaseY, hideCounter);
            return;
        }

        if (hideCounter)
        {
            return;
        }

        Text.Font = GameFont.Small;
        float lineHeight = Text.LineHeight;
        Rect bar = new Rect(UI.screenWidth - AlertDrawer.BarWidth, curBaseY - lineHeight, AlertDrawer.BarWidth, lineHeight);
        int fps = FpsActual!();
        int tps = TpsActual!();
        int target = TpsTarget!();
        ReadoutDrawer.DrawSplitBar(bar, $"FPS: {fps}", $"TPS: {tps}({target})");
        curBaseY -= lineHeight;
    }

    private static bool TryUpdateCounters()
    {
        try
        {
            float tickRate = Find.TickManager.TickRateMultiplier;
            TpsTarget!() = (int)Math.Round(tickRate == 0f ? 0f : 60f * tickRate);

            int prevTicks = PrevTicks!();
            if (prevTicks == -1)
            {
                PrevTicks() = GenTicks.TicksAbs;
                PrevTime!() = DateTime.Now;
            }
            else
            {
                DateTime prevTime = PrevTime!();
                DateTime currTime = DateTime.Now;
                if (currTime.Second != prevTime.Second)
                {
                    PrevTime() = currTime;
                    TpsActual!() = GenTicks.TicksAbs - prevTicks;
                    PrevTicks() = GenTicks.TicksAbs;
                    FpsActual!() = PrevFrames!();
                    PrevFrames() = 0;
                }
            }

            PrevFrames!() = PrevFrames() + 1;
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static void DrawVanillaStacked(ref float curBaseY, bool hideCounter)
    {
        if (PrefixMethod == null)
        {
            return;
        }

        if (!hideCounter)
        {
            AlertDrawer.DrawBarBackground(new Rect(UI.screenWidth - AlertDrawer.BarWidth, curBaseY - 52f, AlertDrawer.BarWidth, 52f));
        }

        object[] args = { UI.screenWidth - AlertDrawer.BarWidth, AlertDrawer.BarWidth, curBaseY };
        PrefixMethod.Invoke(null, args);
        curBaseY = (float)args[2];
    }
}

[HarmonyPatch]
public static class Patch_Analyzer_GUIElement_TPS
{
    public static bool Prepare()
    {
        return AccessTools.TypeByName("Analyzer.GUIElement_TPS") != null;
    }

    public static MethodBase TargetMethod()
    {
        return AccessTools.Method("Analyzer.GUIElement_TPS:Prefix");
    }

    public static bool Prefix()
    {
        return !UiPlusMod.Enabled;
    }
}
