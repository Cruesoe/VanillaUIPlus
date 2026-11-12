using HarmonyLib;
using RimWorld;
using Verse;

namespace VanillaUIPlus;

// Marks a pawn being made a slave, so the join hooks it runs (SetFaction, work settings) pick the slave defaults.
[HarmonyPatch(typeof(Pawn_GuestTracker), nameof(Pawn_GuestTracker.SetGuestStatus))]
public static class Patch_Pawn_GuestTracker_SetGuestStatus
{
    public static void Prefix(Pawn ___pawn, GuestStatus guestStatus, ref Pawn? __state)
    {
        __state = NewColonistDefaults.Enslaving;
        if (guestStatus == GuestStatus.Slave)
        {
            NewColonistDefaults.Enslaving = ___pawn;
        }
    }

    public static void Finalizer(Pawn? __state)
    {
        NewColonistDefaults.Enslaving = __state;
    }
}
