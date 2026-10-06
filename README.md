# Vanilla UI+

Small RimWorld UI improvements that stay close to the vanilla look.

## HUD (bottom right)

- Draws alerts, letters, date, weather, speed controls, and play-settings icons as equal-width bars (172px). Alert and letter labels stay on one line with an ellipsis by default; each can be set to wrap instead.
- Optional temperature tint (human comfort band, about 16–26°C), outdoor temperature, day/night clock tint, a precise game clock with minutes, a **Day x** line under the date (first day is Day 1), and a **colony wealth** line with an items/buildings/pawns breakdown on hover.
- Five speed buttons including ultrafast without development mode (can be turned off), key 4, right-click event slowdown, and tick-rate sliders.
- Reverse alert and letter order, hide individual play-settings buttons, or hide the speed buttons while keeping keyboard shortcuts. Optional shortcuts toggle vanilla's temperature overlay (Backslash; disabled when Heat Map is active) and development mode (Numpad Minus).

Turning **Enable custom HUD** off restores vanilla drawing for this section only; the rest of the mod keeps working.

## Custom notifications

Alerts added by Vanilla UI+ that are not part of the base game, plus alert snoozing. Each is toggleable on its own, and all of them work whether or not the custom HUD is enabled.

- **Hostiles present** — pinned above letters when the custom HUD is on, otherwise it sits in the normal alert stack.
- **Bleeding out** — critical alert when a colonist or prisoner will die from blood loss soon.
- **Trader available** — stays up while a trade caravan, or an orbital trade ship you have a working comms console for, can still be traded with, so a trader you were told about but forgot does not quietly leave.
- **Batteries low** — warns when a power grid is draining its batteries and is either below a charge threshold or close to empty. Both thresholds are configurable; grids that are charging are ignored.
- **Hide research bench alert until unlocked** (default on) — vanilla nags that you have no research bench even when research has not made one buildable yet. Only bites with mods that gate the bench behind research.
- **Snoozing** (default on) — right-click an alert to hide that *kind* of alert for a set number of in-game days (default 3). Snoozes are saved with the world. Left-click still jumps to the problem. Turning snoozing off ignores existing snoozes rather than deleting them; **Clear snoozes** removes them for good.

## Pawn inspect pane

Selecting a colonist, slave or prisoner shows a taller inspect pane. The base game's health, mood, schedule and area bars stay on top. The health and mood bars can be colour-coded by condition and mood level. Underneath, the left column shows overall armor, the comfortable temperature range against the temperature where the pawn stands, move and work speed, and damage per second with hit chance (ranged or melee, depending on the equipped weapon). Bleeding and any needs below a set level appear only when they matter. The right column shows skills with passions, and the bottom line shows the current activity and weapon, with the base game's full inspect text on hover. The header adds the pawn's xenotype, gender and age, the Bio tab's rename button for the name and title, and a first aid cross that turns self-tend on and off. Each part can be turned off. The pane steps aside while RimHUD or Better Inspect Pane is active.

Selecting a wild animal lists the meat and leather it gives when butchered, its wool and shearing interval if it can be sheared, and its chance to turn on whoever harms it.

## Growing-zone fertility overlay

Selecting the growing-zone tool automatically turns on the fertility overlay. Cancelling or switching tools restores its previous state. This can be disabled under the Interface settings.

## Schedule shift arrows

Arrows either side of the Schedule tab's timetable move a pawn's whole 24-hour schedule one hour earlier or later, wrapping round midnight, so a night shift can be moved without repainting it. Clicking an arrow in the column header moves everyone in the list. Based on Orion's [Shift Schedule](https://steamcommunity.com/sharedfiles/filedetails/?id=3599388182) (MIT), which is marked incompatible with Vanilla UI+. Turn them off under **Other settings → Pawn tables**.

A thin white line across the Schedule tab's pawn rows marks the current map's local time and moves continuously through the day. Toggle it under **Interface settings → Pawn tables**. When Chronos Pointer is active, Vanilla UI+ leaves the time indicators to that mod.

## Default schedule, work priorities and assignments

The Schedule and Work tabs get a pin to the left of each pawn's copy/paste buttons. Pin a pawn to make their 24-hour schedule (or their work priorities) the default: every pawn that joins the colony afterwards (starting colonists, births, joiners, recruits, slaves) starts with it. Jobs the pinned pawn cannot do are left at the game's normal setting. Clicking a filled pin clears the default. Defaults are stored in the mod settings, so they carry over to new colonies; manage them on the **Defaults** tab in Vanilla UI+ settings.

The Assign tab gets the same pin on the left of each row. It saves everything on that row: hostility response, medical care, apparel, food, drug and reading policies, and carried items, including columns added by other mods through the game's carry system (such as Progression: Ammunition's ammo). Policies are matched by name, so a colony that has no policy with that name keeps the game's default for it. A pawn who can't fight doesn't change the default hostility response.

## New game setup

Sorts the start-new-game scenario list low-tech to high-tech instead of vanilla's arbitrary order, and colors a thin bar next to each entry by the tech level of its starting faction.

The storyteller and world generation pages get a **Set as default** button. The storyteller page saves the storyteller, difficulty (including custom settings) and reload-anytime/commitment choice; the world page saves planet settings, factions, map size and starting season. The seed stays random. Saved choices are filled in the next time those pages open; manage them on the **Defaults** tab in Vanilla UI+ settings.

The quest reward preferences window also gets a **Set as default** button. It saves each faction type's **Accept goodwill** and **Accept honor** choices and applies those preferences when factions are created in future games. They can be cleared from the same **Defaults** settings tab.

## Title screen

Optionally hides the **Tutorial** button and adds a **Continue** button when a save exists. Continue selects the most recently updated save and uses RimWorld's normal game-version and mod-list checks before loading it. These features replace Merthsoft's [Remove Tutorial Button](https://github.com/merthsoft/remove-tutorial-button) and Phoenix's [Continue Button](https://steamcommunity.com/sharedfiles/filedetails/?id=3195912079), which are marked incompatible with Vanilla UI+.

## Map and world search

The map and world search dialogs group their results by kind, such as all steel or every settlement, with a count; click a group to open it, and hover a map group to point at every item in it. The world search can look at every land tile instead of only places and landmarks, matching biomes, terrain, roads, rivers and stone types (the cog button chooses which), and tints the results on the planet. While choosing a destination with a range ring it can stay inside that range.

The arrow beside the world search opens an advanced search: drag conditions such as biome, temperature, growing period, rainfall, stone, roads and rivers, settlement distance, landmarks and tile features into the list, nest them in **All of**, **Any of** and **None of** groups, and save searches to load later. Typed text then narrows the results. Based on the ideas of kathanon's [Improved Map Search](https://steamcommunity.com/sharedfiles/filedetails/?id=3547866455), rewritten for Vanilla UI+ and marked incompatible with it.

## In-game main menu bar

Reorder tabs, move them into a **More** menu, hide them, change their icons, and choose icon-only, text-and-icon, or text-only. Includes a play-settings cog that opens Vanilla UI+ options.

Change options under **Options → Mod options → Vanilla UI+**.
Vanilla UI+ includes Precise Time's minute display directly and is incompatible with the standalone Precise Time mod.
