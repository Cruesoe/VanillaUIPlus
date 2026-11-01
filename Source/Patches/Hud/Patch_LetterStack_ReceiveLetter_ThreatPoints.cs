using HarmonyLib;
using Verse;

namespace VanillaUIPlus;

[HarmonyPatch(typeof(LetterStack), nameof(LetterStack.ReceiveLetter), typeof(Letter), typeof(string), typeof(int), typeof(bool))]
public static class Patch_LetterStack_ReceiveLetter_ThreatPoints
{
    public static void Prefix(Letter let)
    {
        ThreatPoints.Apply(let);
    }
}
