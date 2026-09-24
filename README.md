# Vanilla UI+

Small RimWorld UI improvements that stay close to the vanilla look.

## HUD (bottom right)

- Draws alerts, letters, date, weather, speed controls, and play-settings icons as equal-width bars (172px). Alert and letter labels stay on one line with an ellipsis by default; each can be set to wrap instead.
- Optional temperature tint (human comfort band, about 16–26°C), outdoor temperature, day/night clock tint, a precise game clock with minutes, a **Day x** line under the date (first day is Day 1), and a **colony wealth** line with an items/buildings/pawns breakdown on hover.
- Five speed buttons including ultrafast without development mode, key 4, right-click event slowdown, and tick-rate sliders.
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

## Schedule shift arrows

Arrows either side of the Schedule tab's timetable move a pawn's whole 24-hour schedule one hour earlier or later, wrapping round midnight, so a night shift can be moved without repainting it. Clicking an arrow in the column header moves everyone in the list. Based on Orion's [Shift Schedule](https://steamcommunity.com/sharedfiles/filedetails/?id=3599388182) (MIT), which is marked incompatible with Vanilla UI+. Turn them off under **Other settings → Pawn tables**.

## Default schedule, work priorities and assignments

The Schedule and Work tabs get a pin to the left of each pawn's copy/paste buttons. Pin a pawn to make their 24-hour schedule (or their work priorities) the default: every pawn that joins the colony afterwards (starting colonists, births, joiners, recruits, slaves) starts with it. Jobs the pinned pawn cannot do are left at the game's normal setting. Clicking a filled pin clears the default. Defaults are stored in the mod settings, so they carry over to new colonies; manage them on the **Defaults** tab in Vanilla UI+ settings.

The Assign tab gets the same pin on the left of each row. It saves everything on that row: hostility response, medical care, apparel, food, drug and reading policies, and carried items, including columns added by other mods through the game's carry system (such as Progression: Ammunition's ammo). Policies are matched by name, so a colony that has no policy with that name keeps the game's default for it. A pawn who can't fight doesn't change the default hostility response.

## New game setup

Sorts the start-new-game scenario list low-tech to high-tech instead of vanilla's arbitrary order, and colors a thin bar next to each entry by the tech level of its starting faction.

The storyteller and world generation pages get a **Set as default** button. The storyteller page saves the storyteller, difficulty (including custom settings) and reload-anytime/commitment choice; the world page saves planet settings, factions, map size and starting season. The seed stays random. Saved choices are filled in the next time those pages open; manage them on the **Defaults** tab in Vanilla UI+ settings.

The quest reward preferences window also gets a **Set as default** button. It saves each faction type's **Accept goodwill** and **Accept honor** choices and applies those preferences when factions are created in future games. They can be cleared from the same **Defaults** settings tab.

## Title screen

Optionally hides the **Tutorial** button and adds a **Continue** button when a save exists. Continue selects the most recently updated save and uses RimWorld's normal game-version and mod-list checks before loading it. These features replace Merthsoft's [Remove Tutorial Button](https://github.com/merthsoft/remove-tutorial-button) and Phoenix's [Continue Button](https://steamcommunity.com/sharedfiles/filedetails/?id=3195912079), which are marked incompatible with Vanilla UI+.

## In-game main menu bar

Reorder tabs, move them into a **More** menu, hide them, change their icons, and choose icon-only, text-and-icon, or text-only. Includes a play-settings cog that opens Vanilla UI+ options.

Change options under **Options → Mod options → Vanilla UI+**.
Vanilla UI+ includes Precise Time's minute display directly and is incompatible with the standalone Precise Time mod.
