using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace VanillaUIPlus;

// Unforbids every haulable item on the map; inactive while Keyz' Allow Utilities or Allow Tool, which have their own, is loaded.
public static class UnforbidAllHotkey
{
    private const string KeyzAllowUtilitiesPackageId = "keyz182.KeyzAllowUtilities";
    private const string AllowToolPackageId = "unlimitedhugs.allowtool";

    // The active mod list cannot change without a restart, so these are resolved once.
    private static bool? keyzAllowUtilitiesActive;
    private static bool? allowToolActive;

    public static bool KeyzAllowUtilitiesActive => keyzAllowUtilitiesActive ??= ModsConfig.IsActive(KeyzAllowUtilitiesPackageId);
    public static bool AllowToolActive => allowToolActive ??= ModsConfig.IsActive(AllowToolPackageId);
    public static bool HandledByOtherMod => KeyzAllowUtilitiesActive || AllowToolActive;

    public static void HandleKeys()
    {
        if (!UiPlusMod.Settings.enableUnforbidAllHotkey || HandledByOtherMod)
        {
            return;
        }

        Map? map = Find.CurrentMap;
        if (map == null || !WorldRendererUtility.DrawingMap || Event.current.type != EventType.KeyDown || !VUIPDefOf.VUIP_UnforbidAll.KeyDownEvent)
        {
            return;
        }

        Event.current.Use();
        UnforbidAll(map);
    }

    private static void UnforbidAll(Map map)
    {
        int count = 0;
        foreach (Thing thing in map.listerThings.AllThings)
        {
            if (thing is not ThingWithComps thingWithComps || thing.def is not { EverHaulable: true } || map.fogGrid.IsFogged(thing.Position))
            {
                continue;
            }

            CompForbiddable? comp = thingWithComps.GetComp<CompForbiddable>();
            if (comp is not { Forbidden: true })
            {
                continue;
            }

            comp.Forbidden = false;
            count += thing.stackCount;
        }

        TaggedString message = count > 0
            ? "VUIP.UnforbidAllDone".Translate(count)
            : "VUIP.UnforbidAllNone".Translate();
        Messages.Message(message, MessageTypeDefOf.NeutralEvent, historical: false);
    }
}
