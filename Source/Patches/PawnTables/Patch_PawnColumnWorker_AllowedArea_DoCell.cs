using HarmonyLib;
using RimWorld;

namespace VanillaUIPlus;

[HarmonyPatch(typeof(PawnColumnWorker_AllowedArea), nameof(PawnColumnWorker_AllowedArea.DoCell))]
public static class Patch_PawnColumnWorker_AllowedArea_DoCell
{
    public static void Prefix(PawnTable table)
    {
        AreaAllowedTable.Current = table;
    }
}
