using RimWorld;
using Verse;

namespace VanillaUIPlus;

[DefOf]
public static class VUIPDefOf
{
    public static KeyBindingDef VUIP_UnforbidAll = null!;

    static VUIPDefOf()
    {
        DefOfHelper.EnsureInitializedInCtor(typeof(VUIPDefOf));
    }
}
