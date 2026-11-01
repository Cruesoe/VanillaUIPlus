using System.Reflection;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace VanillaUIPlus;

[HarmonyPatch(typeof(ITab_Pawn_Character), "UpdateSize")]
public static class Patch_ITab_Pawn_Character_UpdateSize
{
    private static readonly FieldInfo? SizeField = AccessTools.Field(typeof(ITab), "size");
    private static readonly AccessTools.FieldRef<ITab, Vector2>? Size =
        ReflectionGuard.FieldRef<ITab, Vector2>(nameof(ITab_Pawn_Character), "size", SizeField);

    public static void Postfix(ITab_Pawn_Character __instance)
    {
        if (Size == null)
        {
            return;
        }

        Pawn? pawn = CharacterTabSize.SelectedPawn();
        Size(__instance) = CharacterTabSize.Expand(Size(__instance), pawn);
    }
}
