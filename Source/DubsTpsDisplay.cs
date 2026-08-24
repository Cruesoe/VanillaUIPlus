using System;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using Verse;

namespace VanillaUIPlus;

public static class DubsTpsDisplay
{
    private static readonly Type? TpsType;
    private static readonly FieldInfo? DisableField;
    private static readonly FieldInfo? PrevTimeField;
    private static readonly FieldInfo? PrevTicksField;
    private static readonly FieldInfo? TpsActualField;
    private static readonly FieldInfo? TpsTargetField;
    private static readonly FieldInfo? PrevFramesField;
    private static readonly FieldInfo? FpsActualField;
    private static readonly PropertyInfo? CurrentlyProfilingProp;
    private static readonly MethodInfo? PrefixMethod;
    private static readonly bool CanUpdate;

    static DubsTpsDisplay()
    {
        TpsType = AccessTools.TypeByName("Analyzer.GUIElement_TPS");
        PrefixMethod = TpsType == null
            ? null
            : AccessTools.Method(TpsType, "Prefix", new[] { typeof(float), typeof(float), typeof(float).MakeByRefType() });
        Type? settingsType = AccessTools.TypeByName("Analyzer.Settings");
        DisableField = settingsType == null ? null : AccessTools.Field(settingsType, "disableTPSCounter");
        Type? analyzerType = AccessTools.TypeByName("Analyzer.Profiling.Analyzer");
        CurrentlyProfilingProp = analyzerType == null ? null : AccessTools.Property(analyzerType, "CurrentlyProfiling");

        if (TpsType != null)
        {
            PrevTimeField = AccessTools.Field(TpsType, "prevTime");
            PrevTicksField = AccessTools.Field(TpsType, "prevTicks");
            TpsActualField = AccessTools.Field(TpsType, "tpsActual");
            TpsTargetField = AccessTools.Field(TpsType, "tpsTarget");
            PrevFramesField = AccessTools.Field(TpsType, "prevFrames");
            FpsActualField = AccessTools.Field(TpsType, "fpsActual");
        }

        CanUpdate = PrevTimeField != null
            && PrevTicksField != null
            && TpsActualField != null
            && TpsTargetField != null
            && PrevFramesField != null
            && FpsActualField != null;
    }

    public static void Draw(ref float curBaseY)
    {
        if (TpsType == null)
        {
            return;
        }

        bool hideCounter = DisableField != null && (bool)DisableField.GetValue(null);
        bool profiling = CurrentlyProfilingProp != null && (bool)CurrentlyProfilingProp.GetValue(null);
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
        int fps = (int)FpsActualField!.GetValue(null);
        int tps = (int)TpsActualField!.GetValue(null);
        int target = (int)TpsTargetField!.GetValue(null);
        ReadoutDrawer.DrawSplitBar(bar, $"FPS: {fps}", $"TPS: {tps}({target})");
        curBaseY -= lineHeight;
    }

    private static bool TryUpdateCounters()
    {
        try
        {
            float tickRate = Find.TickManager.TickRateMultiplier;
            TpsTargetField!.SetValue(null, (int)Math.Round(tickRate == 0f ? 0f : 60f * tickRate));

            int prevTicks = (int)PrevTicksField!.GetValue(null);
            if (prevTicks == -1)
            {
                PrevTicksField.SetValue(null, GenTicks.TicksAbs);
                PrevTimeField!.SetValue(null, DateTime.Now);
            }
            else
            {
                DateTime prevTime = (DateTime)PrevTimeField!.GetValue(null);
                DateTime currTime = DateTime.Now;
                if (currTime.Second != prevTime.Second)
                {
                    PrevTimeField.SetValue(null, currTime);
                    TpsActualField!.SetValue(null, GenTicks.TicksAbs - prevTicks);
                    PrevTicksField.SetValue(null, GenTicks.TicksAbs);
                    FpsActualField!.SetValue(null, (int)PrevFramesField!.GetValue(null));
                    PrevFramesField.SetValue(null, 0);
                }
            }

            PrevFramesField!.SetValue(null, (int)PrevFramesField.GetValue(null) + 1);
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
