# Close the Deal — Game Design Document

Oct 3, 2026 · @Adam

## Overview

A man in a suit is blown out of a 14th-floor window by a rocket. As he falls past the glass, the secretary announces the quarterly figures.

Two rival companies race up two skyscrapers side by side, shooting at each other through the windows. The first team to reach the roof wins. The game launches on Steam in Early Access on 29 Oct 2026.

- **The pitch:** you and a mate against another pair. Window wars across the street, zipline raids into the enemy tower, furniture and businessmen flying.
- **The voice:** a deadpan secretary narrates every run. She is the game's personality and the thing people quote.
- **The feel:** chaotic, not precise. Explosions are funny. Falling behind is a setup for a comeback, not a loss.
- **Genre:** team vs team, short replayable runs of 10–15 minutes, built for friend groups and the people watching them.
- **Engine:** Unity 6, host-authoritative peer-to-peer over Steam.
- **Working title:** Close the Deal (not final).

### The three moments

These are what a trailer, a store gif and a streamer clip are built from. Everything in v1 serves at least one of them.

1. **The window shot.** A rocket through the glass, a ragdolled businessman out the other side, desks and monitors following him.
2. **The zipline raid.** Fully exposed on the wire, then landing in the enemy's floor and wrecking it while they're still looking across the gap.
3. **The secretary.** A flat corporate voice reading out a death, a checkpoint or a defeat as if it were a calendar reminder.

## Core loop and win condition

The first team to reach the roof wins. Each run follows the same loop:

1. Start in the company lobby. The secretary starts the run and hands out starting kit.
2. Climb floor by floor. Find loot, keycards and rare items on the way up.
3. Fight across the gap through the windows to slow the other team down.
4. Reach checkpoint floors, which save progress and reveal how far the rival team has got.
5. Take on the fixed boardroom, the final big fight, then reach the roof to win.

The tension comes from trading speed against exploration. Rushing upwards keeps you ahead. Exploring a floor finds the items that let a team skip floors or raid the enemy.

## The buildings

Each team gets its own skyscraper, built from random floor templates between fixed progression points. This gives replayability without the building feeling incoherent.

| Floor type | Placement | Purpose |
|---|---|---|
| Lobby / reception | Fixed, ground | Social space, run start, secretary |
| Random floors | Stacked from templates | Most of the climb; layout changes each run |
| Checkpoint floors | Fixed intervals | Save progress, show rival team's progress |
| Special floors | Fixed points | Set-piece moments, harder sections |
| Boardroom / executive | Fixed, near top | Final major fight |
| Roof | Fixed, top | Win point |

### Standard floor layout

Every random floor uses the same shell (decided 2026-09-28):

```
          RIVAL TOWER
               ↓
┌───────────────────────────────┐
│                               │
│          MAIN FLOOR           │
│                               │
│    ┌───────┐     ┌───────┐    │
│    │ GLASS │     │ GLASS │    │
│    │ ROOM  │     │ ROOM  │    │
│    └───────┘     └───────┘    │
│                               │
├──────── SERVICE SPINE ────────┤
│ WC │ STORE │ STAIRS ↑ │ UTIL  │
└───────────────────────────────┘
```

- **Main floor** faces the rival tower. It is always open: you can clearly see where every player on it is. Templates vary this area.
- **Glass rooms** show who is inside but give no protection.
- **Service spine** sits at the back and is the same on every floor, so the stairs always line up and random floors stack cleanly.
- **WC and UTIL** can be fully covered. A player inside is hidden from the rival, but in turn can't see what the rival is doing.
- Floor depth is set in the greybox so the back of the main floor stays within cross-gap weapon range.

### Rules for templates

- The building shell is not destructible. Only the contents are.
- Every template uses standard window bays on the side that faces the other building, so sightlines always line up across the gap.
- Players should generally be visible through the windows, especially on important floors. The main floor is always visible; only the service spine rooms may be fully hidden.
- Both buildings use the same seed each run, so neither team gets an easier layout. (Proposed; see open questions.)
- Sections get harder and more absurd the higher you go.

## Combat, weapons and physics

Most fighting happens across the gap through the windows, and it should feel chaotic rather than precise. Explosions throw players and office furniture around; they don't bring down walls.

Glass never stops a projectile. Every bullet, rocket and throwable goes straight through windows and glass-room walls.

| Item | Role | In v1 |
|---|---|---|
| Pistol / rifle | Reliable cross-gap shooting | Yes |
| Rocket launcher | Knockback, scatters props, ragdolls players | Yes |
| Throwable explosive | Lobbed through windows to flush out a floor | Yes |
| Zipline gun | Rare; cross to the enemy building for a raid | Yes (showpiece) |
| Roguelite weapons and gadgets | Weird run-altering kit | Roadmap |

- **Physics props:** chairs, desks, computers and filing cabinets can be blown around. Cap the number of simulated props per floor and let the host own every prop.
- **Ragdolls:** players ragdoll on explosions and heavy hits, then recover quickly. It should be funny, not a long stun.
- **Zipline raids:** the player is fully exposed while crossing. Landing in the enemy building allows hit-and-run attacks, kills, stealing resources and general chaos.

## Comebacks and rare items

The game has no stat rubber-banding. A team that falls behind catches up through powerful rare items and shortcuts. Being ahead is an advantage, not a guarantee.

- **Elevators + access cards:** rare elevators skip several floors, but each one needs a matching access card found on the floor. Using one makes a loud lift noise that alerts the other team. (Roadmap.)
- **Zipline gun:** lets a trailing team raid the leaders directly.
- **Other rare items:** planned for later, as part of the roguelite pool.

Open design point: how strongly these items should favour the trailing team, for example through higher drop rates or items that only appear when behind.

## Lobby and the secretary

The secretary is the game's voice. She is the main interface, the narrator and the personality the marketing leans on. Every run starts, turns and ends on one of her lines.

Each team starts in its company's reception area.

- **v1:** a simple lobby where talking to the secretary starts the run and hands over starting kit. She has a small set of deadpan lines at the start, at checkpoints and on win or loss.
- **Roadmap:** character customisation, outfits, cosmetics, emotes and weapon loadouts in the lobby; a full commentary system through the run; the secretary managing upgrades physically instead of through menus.

## Art and sound direction

All art is original. No asset-store packs ship in the game (decided 2026-10-03). Packs may still stand in during greybox work and are replaced before the store page screenshots are taken.

The look itself is not yet set. It is the owner's call and is listed under open questions. Whatever it is, it has to:

- Read instantly in a 20-second gif: two towers, a gap, suits, glass.
- Make a ragdolled businessman funny from across the street, so silhouettes and exaggerated poses matter more than detail.
- Survive 8–10 floor templates being dressed in weeks, so a small palette and a few repeatable prop families, not a hero asset per floor.
- Give the secretary a face or a desk that can sit on the capsule art.

Sound carries the comedy: glass, screenshake, a long scream on a window exit, and the secretary's flat delivery over all of it.

## Technical architecture

The host owns everything that matters, and clients only send input. This keeps a physics-heavy game workable with no dedicated servers.

- **Networking:** host-authoritative peer-to-peer over Steam, using **FishNet with the FishySteamworks transport** (decided 2026-09-28). FishNet has built-in client prediction, so knockback feels instant on a client rather than a round-trip late. Fallback if prediction fights us in week 1: the host decides all knockback, unpredicted.
- **Host leaving:** if the host quits, the run ends. There is no host migration.
- **Steam:** lobbies, friend invites and join-in-progress into the lobby only.
- **Authority:** the host simulates props, ragdoll triggers, projectiles, item spawns and floor generation.
- **Floor generation:** the host picks a seed and sends it to clients, which build the same floors locally.
- **Physics budget:** a fixed cap on simulated props per floor, props sleep when still, and floors far from any player are frozen or unloaded.
- **Ragdolls:** sync the trigger and impulse, then blend back to the animated character. Don't stream every bone.
- **Building shells:** static geometry only, so there's no structural destruction to sync.

**Biggest technical risk:** syncing props and ragdolls. If it isn't stable by the end of week 1, cut the number of props before cutting anything else.

## v1 launch scope

This is the Early Access build for 29 Oct. Anything not listed here is on the roadmap.

- [ ] 2v2 teams, two buildings, Steam lobby and invites
- [ ] Movement, climbing and cross-gap shooting that feel good
- [ ] 8–10 random floor templates with shared window bays
- [ ] 1 checkpoint floor showing the rival team's progress
- [ ] Fixed boardroom final fight + roof win
- [ ] Pistol/rifle, rocket launcher, throwable explosive, zipline gun
- [ ] Physics props (capped) and explosion ragdolls
- [ ] Lobby with a secretary who starts the run and has a few deadpan lines
- [ ] Results screen and rematch
- [ ] Original office art style (characters, floor dressing, props, UI), plus audio and screenshake
- [ ] Store page: capsule art, 5+ screenshots, description, and an optional short trailer

## Early Access roadmap

These come after launch, roughly in priority order. Update 1 should add the features players miss most.

| Priority | Feature | Why |
|---|---|---|
| 1 | Elevators + access cards, with loud lift alert | Core comeback mechanic, cheap to add |
| 2 | Roguelite upgrades: weird weapons, movement, explosives, zipline mods, corporate gear | Main replayability driver |
| 3 | More floor templates and difficulty sections | Freshness per run |
| 4 | Resource stealing on zipline raids | Deepens raids |
| 5 | Cosmetics, outfits, emotes in the lobby | Social layer, retention |
| 6 | Full secretary commentary system | Personality, marketing moments |
| 7 | 3v3 / 4v4 | Bigger lobbies once netcode is proven |

## Schedule and Steam milestones

The store page deadline on 7 Oct is the one that can't slip. It starts the 14-day Coming Soon clock that release depends on.

Steam rules: no release until 30 days after the app fee is paid; the Coming Soon page must be live for at least 14 days; store page and build reviews take 3–5 business days each, so allow 7. Sources: Steam Direct, Release process.

### Publisher pitch

Target: Oro Interactive. **Parked until the MVP exists (decided 2026-10-03).** They sign on a moment, not a document, and say so publicly. When the time comes, send things in this order and send this GDD only when asked for more.

1. A 15–20 second gif of the window shot (moment 1), captured from the real art, not the greybox.
2. The live Steam Coming Soon page.
3. Half a page: the one-line pitch, the three moments, player count, price, EA date, team size.
4. A playable build with a second machine ready for them to join.

Oro's catalogue is £5–£10 friend-group games with one odd hook. Pitch it as "you and a mate against another pair", not as a shooter.

## Open questions

- [ ] **Art direction.** Original art is decided; the look is not. Needs a one-line style statement and a reference board before any art is made. Blocks the store page screenshots and the pitch gif.
- [ ] Confirm final name (working title: Close the Deal)
- [x] ~~FishNet or Netcode for GameObjects + Steam transport?~~ FishNet + FishySteamworks.
- [ ] Are the stairs in the service spine visible to the rival, or hidden like the WC and UTIL?
- [ ] First-person or third-person camera? The greybox uses third-person over the shoulder so knockback and ragdolls can be seen.
- [ ] Does "climbing" mean more than mantling ledges (ladders, pipes, the outside of the building)?
- [ ] Same seed for both buildings, or different layouts with balancing?
- [ ] How many floors per run, and how long should a run take (target 10–15 min?)
- [ ] Is respawn on death at the last checkpoint, or on the same floor after a delay?
- [ ] Can a zipline raider be sent back, for example by cutting the line?
- [ ] Price point and Early Access length. Oro's range is £5–£10; a publisher pitch needs a number.
- [ ] Can a run start 1v1 or 2v1, or does it always wait for four? Affects how often friend groups can actually play.
- [ ] Is the Steamworks fee paid, and on what date? This sets the earliest release date.
