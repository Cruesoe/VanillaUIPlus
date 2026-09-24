using RimWorld;
using Verse;

namespace VanillaUIPlus;

[DefOf]
public static class VUIPDefOf
{
    public static KeyBindingDef VUIP_UnforbidAll = null!;
    public static KeyBindingDef VUIP_ToggleTemperatureOverlay = null!;

    static VUIPDefOf()
    {
        DefOfHelper.EnsureInitializedInCtor(typeof(VUIPDefOf));
    }
}
