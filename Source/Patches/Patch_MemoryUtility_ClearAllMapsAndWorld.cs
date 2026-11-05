using HarmonyLib;

namespace VanillaUIPlus;

// The base game calls this when a game is unloaded; per-game caches are cleared with it.
[HarmonyPatch(typeof(Verse.Profile.MemoryUtility), nameof(Verse.Profile.MemoryUtility.ClearAllMapsAndWorld))]
public static class Patch_MemoryUtility_ClearAllMapsAndWorld
{
    public static void Postfix()
    {
        DesignatorOverlay.RestoreAll();
        PawnReadout.Forget();
        PawnReadoutDrawer.Forget();
        PawnReadoutDrawer.ForgetCreature();
        GrowingZoneReadout.Forget();
        GearSnapshot.Forget();
        WildAnimalYields.Forget();
        AlertDrawer.Forget();
        Alert_HostilesPresent.Forget();
        WorldDominationThreat.Forget();
        WorldSearchHighlight.Forget();
        CharacterTabSize.Forget();
        SnoozeTracker.Forget();
        ResourceReadoutTweaks.Forget();
        AreaAllowedTable.Current = null;
        MapSearchUi.Forget();
        MainButtonPainter.Forget();
    }
}
