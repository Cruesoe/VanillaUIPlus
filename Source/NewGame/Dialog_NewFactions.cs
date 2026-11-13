using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace VanillaUIPlus;

/// <summary>
/// Lists factions the world default doesn't know about; ticked ones are added to this world and the default, and all listed are remembered.
/// </summary>
public class Dialog_NewFactions : Window
{
    // WorldFactionsUIUtility's limit on factions shown in the world's faction list (verified on 1.7).
    private const int MaxVisibleFactions = 12;
    private const float RowHeight = 30f;
    private const float IconSize = 24f;
    private const float ButtonHeight = 35f;

    private readonly WorldDefaults defaults;
    private readonly List<FactionDef> factions;
    private readonly List<FactionDef> initialFactions;
    private readonly List<Entry> entries = new List<Entry>();
    private readonly string intro;
    private string acceptLabel = string.Empty;
    private Vector2 scroll;
    private float introHeight = -1f;
    private float introWidth;

    public Dialog_NewFactions(
        WorldDefaults defaults,
        List<FactionDef> offered,
        List<FactionDef> gameFactions,
        List<FactionDef> factions,
        List<FactionDef> initialFactions,
        bool legacy)
    {
        this.defaults = defaults;
        this.factions = factions;
        this.initialFactions = initialFactions;
        intro = (legacy ? "VUIP.NewFactionsLegacy" : "VUIP.NewFactionsIntro").Translate();

        string limitTip = "VUIP.NewFactionsLimit".Translate(MaxVisibleFactions);
        foreach (FactionDef faction in offered)
        {
            // As many copies as a fresh world starts with, or one for a faction the game adds only on request.
            int copies = Mathf.Max(1, gameFactions.Count(f => f == faction));
            FactionDef? replaced = faction.replacesFaction;
            string label = faction.LabelCap;
            if (copies > 1)
            {
                label += " x" + copies;
            }

            if (replaced != null)
            {
                label += " (" + "VUIP.NewFactionReplaces".Translate(replaced.label) + ")";
            }

            entries.Add(new Entry(faction, replaced, copies, label, faction.description ?? string.Empty, limitTip)
            {
                ticked = !legacy && gameFactions.Contains(faction)
            });
        }

        // Untick from the bottom until the starting choice fits the limit.
        for (int i = entries.Count - 1; i >= 0 && VisibleCount(null) > MaxVisibleFactions; i--)
        {
            entries[i].ticked = false;
        }

        Refresh();

        forcePause = true;
        doCloseX = true;
        closeOnClickedOutside = false;
        absorbInputAroundWindow = true;
    }

    public override Vector2 InitialSize => new Vector2(520f, Mathf.Min(600f, 200f + entries.Count * RowHeight));

    public override void DoWindowContents(Rect inRect)
    {
        Text.Font = GameFont.Medium;
        Rect title = new Rect(inRect.x, inRect.y, inRect.width, 36f);
        Widgets.Label(title, "VUIP.NewFactionsTitle".Translate());
        Text.Font = GameFont.Small;

        if (introHeight < 0f || introWidth != inRect.width)
        {
            introWidth = inRect.width;
            introHeight = Text.CalcHeight(intro, inRect.width);
        }

        Rect introRect = new Rect(inRect.x, title.yMax + 4f, inRect.width, introHeight);
        Widgets.Label(introRect, intro);

        Rect outRect = new Rect(inRect.x, introRect.yMax + 8f, inRect.width, inRect.yMax - ButtonHeight - 8f - (introRect.yMax + 8f));
        Rect viewRect = new Rect(0f, 0f, outRect.width - 16f, entries.Count * RowHeight);
        Widgets.BeginScrollView(outRect, ref scroll, viewRect);
        for (int i = 0; i < entries.Count; i++)
        {
            DrawRow(new Rect(0f, i * RowHeight, viewRect.width, RowHeight), entries[i]);
        }

        Widgets.EndScrollView();

        Rect buttons = new Rect(inRect.x, inRect.yMax - ButtonHeight, inRect.width, ButtonHeight);
        if (Widgets.ButtonText(buttons.LeftHalf().ContractedBy(4f, 0f), "VUIP.NewFactionsLater".Translate()))
        {
            Close();
        }

        if (Widgets.ButtonText(buttons.RightHalf().ContractedBy(4f, 0f), acceptLabel))
        {
            Accept();
        }
    }

    private void DrawRow(Rect row, Entry entry)
    {
        bool blocked = !entry.ticked && !entry.fits;
        if (Mouse.IsOver(row))
        {
            Widgets.DrawHighlight(row);
        }

        TooltipHandler.TipRegion(row, blocked ? entry.limitTip : entry.description);

        Rect icon = new Rect(row.x + 4f, row.y + (row.height - IconSize) / 2f, IconSize, IconSize);
        GUI.color = blocked ? entry.faction.DefaultColor * Widgets.InactiveColor : entry.faction.DefaultColor;
        GUI.DrawTexture(icon, entry.faction.FactionIcon);
        GUI.color = blocked ? Widgets.InactiveColor : Color.white;

        Rect label = new Rect(icon.xMax + 8f, row.y, row.width - IconSize - 48f, row.height);
        Text.Anchor = TextAnchor.MiddleLeft;
        Widgets.Label(label, entry.label);
        Text.Anchor = TextAnchor.UpperLeft;
        GUI.color = Color.white;

        bool ticked = entry.ticked;
        Widgets.Checkbox(row.xMax - 28f, row.y + (row.height - 24f) / 2f, ref ticked, disabled: blocked);
        if (!blocked && Widgets.ButtonInvisible(label))
        {
            ticked = !ticked;
            (ticked ? SoundDefOf.Checkbox_TurnedOn : SoundDefOf.Checkbox_TurnedOff).PlayOneShotOnCamera();
        }

        if (ticked != entry.ticked)
        {
            entry.ticked = ticked;
            Refresh();
        }
    }

    // Recomputed only when a tick changes.
    private void Refresh()
    {
        foreach (Entry entry in entries)
        {
            entry.fits = entry.ticked || VisibleCount(entry) <= MaxVisibleFactions;
        }

        int count = entries.Count(e => e.ticked);
        acceptLabel = count > 0 ? "VUIP.NewFactionsAdd".Translate(count) : "VUIP.NewFactionsSkip".Translate();
    }

    // Visible factions this world would have with the ticked entries, plus extra if given.
    private int VisibleCount(Entry? extra)
    {
        List<FactionDef> result = new List<FactionDef>(factions);
        foreach (Entry entry in entries)
        {
            if (entry.ticked || entry == extra)
            {
                AddTo(result, entry);
            }
        }

        return result.Count(f => !f.hidden);
    }

    private static void AddTo(List<FactionDef> list, Entry entry)
    {
        if (entry.replaced != null)
        {
            list.RemoveAll(f => f == entry.replaced);
        }

        for (int i = 0; i < entry.copies; i++)
        {
            list.Add(entry.faction);
        }
    }

    private void Accept()
    {
        int added = 0;
        foreach (Entry entry in entries)
        {
            if (!entry.ticked)
            {
                continue;
            }

            AddTo(factions, entry);
            if (entry.replaced != null)
            {
                defaults.factions.RemoveAll(name => name == entry.replaced.defName);
            }

            for (int i = 0; i < entry.copies; i++)
            {
                defaults.factions.Add(entry.faction.defName);
            }

            added++;
        }

        // The page compares against initialFactions to detect changes.
        initialFactions.Clear();
        initialFactions.AddRange(factions);

        defaults.knownFactions = NewFactionOffer.SelectableFactionNames();
        UiPlusMod.Instance.WriteSettings();
        if (added > 0)
        {
            Messages.Message("VUIP.NewFactionsAdded".Translate(), MessageTypeDefOf.PositiveEvent, historical: false);
        }

        Close();
    }

    private class Entry
    {
        public readonly FactionDef faction;
        public readonly FactionDef? replaced;
        public readonly int copies;
        public readonly string label;
        public readonly string description;
        public readonly string limitTip;
        public bool ticked;
        public bool fits;

        public Entry(FactionDef faction, FactionDef? replaced, int copies, string label, string description, string limitTip)
        {
            this.faction = faction;
            this.replaced = replaced;
            this.copies = copies;
            this.label = label;
            this.description = description;
            this.limitTip = limitTip;
        }
    }
}
