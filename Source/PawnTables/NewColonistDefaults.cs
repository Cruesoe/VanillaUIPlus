using Verse;

namespace VanillaUIPlus;

/// <summary>
/// Join hooks shared by the pinned defaults (schedule, work priorities, assignments); hooks only one default needs stay with it.
/// </summary>
public static class NewColonistDefaults
{
    // Uses Faction.IsPlayer rather than Faction.OfPlayer, which isn't safe while a save loads.
    public static bool IsPlayerPawn(Pawn pawn)
    {
        return pawn.Faction != null && pawn.Faction.IsPlayer;
    }

    // The pawn whose SetGuestStatus is making them a slave; set only for that call, while it joins the faction.
    public static Pawn? Enslaving;

    // Colonists and slaves; not colony mechs or mutants, whose work and policies the game handles itself.
    public static bool IsColonyHumanlike(Pawn pawn)
    {
        return pawn.RaceProps.Humanlike && !pawn.IsMutant && IsPlayerPawn(pawn);
    }

    // Enslaving joins the faction before the slave status is set, so IsSlave alone misses it.
    public static bool JoinsAsSlave(Pawn pawn)
    {
        return pawn.IsSlave || Enslaving == pawn;
    }

    public struct JoinState
    {
        public bool wasPlayer;
        public bool wasPrisoner;
    }
}
