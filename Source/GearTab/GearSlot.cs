using System.Collections.Generic;
using RimWorld;
using Verse;

namespace VanillaUIPlus;

/// <summary>
/// A place on the body apparel goes: body part groups plus layers. Slots no loaded apparel can fill are left out.
/// </summary>
public sealed class GearSlot
{
    public readonly string Label;
    public readonly string Tip;

    // Null groups match any apparel on the layers; null layers match any layer.
    public readonly List<BodyPartGroupDef>? Groups;
    public readonly List<ApparelLayerDef>? Layers;

    // Body part group the base game's nudity checks look at for this slot, if any.
    public readonly BodyPartGroupDef? NudityGroup;

    private static List<GearSlot>? all;
    private static LoadedLanguage? allLanguage;

    private GearSlot(string key, List<BodyPartGroupDef>? groups, List<ApparelLayerDef>? layers, BodyPartGroupDef? nudityGroup)
    {
        Label = key.Translate();
        Tip = (key + "Tip").Translate();
        Groups = groups;
        Layers = layers;
        NudityGroup = nudityGroup;
    }

    /// <summary>Slots some loaded apparel can fill; rebuilt when play data reloads (a language switch reloads every def).</summary>
    public static List<GearSlot> All
    {
        get
        {
            LoadedLanguage language = LanguageDatabase.activeLanguage;
            if (all == null || language != allLanguage)
            {
                allLanguage = language;
                all = Build();
            }

            return all;
        }
    }

    public bool Fits(ThingDef def)
    {
        ApparelProperties? apparel = def.apparel;
        if (apparel == null)
        {
            return false;
        }

        if (Groups != null && !AnyShared(Groups, apparel.bodyPartGroups))
        {
            return false;
        }

        return Layers == null || AnyShared(Layers, apparel.layers);
    }

    /// <summary>Whether the pawn has an unmissing body part in this slot's groups.</summary>
    public bool PawnHasParts(Pawn pawn)
    {
        if (Groups == null)
        {
            return true;
        }

        foreach (BodyPartRecord part in pawn.health.hediffSet.GetNotMissingParts())
        {
            for (int i = 0; i < Groups.Count; i++)
            {
                if (part.IsInGroup(Groups[i]))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static bool AnyShared<T>(List<T> a, List<T>? b)
        where T : class
    {
        if (b == null)
        {
            return false;
        }

        for (int i = 0; i < a.Count; i++)
        {
            if (b.Contains(a[i]))
            {
                return true;
            }
        }

        return false;
    }

    private static List<GearSlot> Build()
    {
        BodyPartGroupDef? fullHead = DefDatabase<BodyPartGroupDef>.GetNamedSilentFail("FullHead");
        BodyPartGroupDef? upperHead = DefDatabase<BodyPartGroupDef>.GetNamedSilentFail("UpperHead");
        BodyPartGroupDef? torso = DefDatabase<BodyPartGroupDef>.GetNamedSilentFail("Torso");
        BodyPartGroupDef? legs = DefDatabase<BodyPartGroupDef>.GetNamedSilentFail("Legs");
        BodyPartGroupDef? feet = DefDatabase<BodyPartGroupDef>.GetNamedSilentFail("Feet");
        BodyPartGroupDef? hands = DefDatabase<BodyPartGroupDef>.GetNamedSilentFail("Hands");
        ApparelLayerDef? onSkin = DefDatabase<ApparelLayerDef>.GetNamedSilentFail("OnSkin");
        ApparelLayerDef? middle = DefDatabase<ApparelLayerDef>.GetNamedSilentFail("Middle");
        ApparelLayerDef? shell = DefDatabase<ApparelLayerDef>.GetNamedSilentFail("Shell");
        ApparelLayerDef? overhead = DefDatabase<ApparelLayerDef>.GetNamedSilentFail("Overhead");
        ApparelLayerDef? belt = DefDatabase<ApparelLayerDef>.GetNamedSilentFail("Belt");

        List<GearSlot> candidates = new List<GearSlot>();
        if (overhead != null && (fullHead != null || upperHead != null))
        {
            candidates.Add(new GearSlot("VUIP.GearSlotHead", NonNull(fullHead, upperHead), NonNull(overhead), upperHead));
        }

        if (torso != null)
        {
            if (onSkin != null)
            {
                candidates.Add(new GearSlot("VUIP.GearSlotShirt", NonNull(torso), NonNull(onSkin), torso));
            }

            if (middle != null)
            {
                candidates.Add(new GearSlot("VUIP.GearSlotMiddle", NonNull(torso), NonNull(middle), null));
            }

            if (shell != null)
            {
                candidates.Add(new GearSlot("VUIP.GearSlotOuter", NonNull(torso), NonNull(shell), null));
            }
        }

        if (legs != null && onSkin != null)
        {
            candidates.Add(new GearSlot("VUIP.GearSlotLegs", NonNull(legs), NonNull(onSkin), legs));
        }

        if (feet != null)
        {
            candidates.Add(new GearSlot("VUIP.GearSlotFeet", NonNull(feet), null, null));
        }

        if (hands != null)
        {
            candidates.Add(new GearSlot("VUIP.GearSlotHands", NonNull(hands), null, null));
        }

        if (belt != null)
        {
            candidates.Add(new GearSlot("VUIP.GearSlotUtility", null, NonNull(belt), null));
        }

        List<ThingDef> defs = DefDatabase<ThingDef>.AllDefsListForReading;
        List<GearSlot> result = new List<GearSlot>();
        foreach (GearSlot slot in candidates)
        {
            for (int i = 0; i < defs.Count; i++)
            {
                if (defs[i].IsApparel && slot.Fits(defs[i]))
                {
                    result.Add(slot);
                    break;
                }
            }
        }

        return result;
    }

    private static List<T> NonNull<T>(params T?[] items)
        where T : class
    {
        List<T> list = new List<T>();
        foreach (T? item in items)
        {
            if (item != null)
            {
                list.Add(item);
            }
        }

        return list;
    }
}
