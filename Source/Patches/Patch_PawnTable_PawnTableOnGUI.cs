using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace VanillaUIPlus;

[HarmonyPatch(typeof(PawnTable), nameof(PawnTable.PawnTableOnGUI))]
public static class Patch_PawnTable_PawnTableOnGUI
{
    private const float LineWidth = 2f;
    private static readonly Color LineColor = new Color(1f, 1f, 1f, 0.8f);

    public static bool ChronosPointerActive => ModsConfig.IsActive("coolnether123.chronospointer");

    public static void Postfix(PawnTable __instance, Vector2 position,
        List<float> ___cachedColumnWidths, Vector2 ___scrollPosition)
    {
        Map? map = Find.CurrentMap;
        if (!UiPlusMod.Settings.showScheduleTimeLine || ChronosPointerActive
            || map == null || Event.current.type != EventType.Repaint)
        {
            return;
        }

        List<PawnColumnDef> columns = __instance.Columns;
        if (columns.Count != ___cachedColumnWidths.Count)
        {
            return;
        }

        int headerHeight = (int)__instance.HeaderHeight;
        float height = Mathf.Min((int)__instance.Size.y - headerHeight,
            (int)__instance.HeightNoScrollbar - headerHeight - ___scrollPosition.y);
        if (height <= 0f)
        {
            return;
        }

        int offset = 0;
        float availableWidth = __instance.Size.x - 16f;
        float dayPercent = Mathf.Repeat(GenLocalDate.DayPercent(map), 1f);
        for (int i = 0; i < columns.Count; i++)
        {
            // Match the table's integer column widths, including its last-column remainder.
            int width = i == columns.Count - 1
                ? (int)(availableWidth - offset)
                : (int)___cachedColumnWidths[i];
            if (columns[i].Worker is PawnColumnWorker_Timetable && width >= LineWidth)
            {
                float x = (int)position.x + offset
                    + Mathf.Clamp(width * dayPercent - LineWidth / 2f, 0f, width - LineWidth);
                Widgets.DrawBoxSolid(new Rect(x, (int)position.y + headerHeight, LineWidth, height), LineColor);
            }

            offset += width;
        }
    }
}
