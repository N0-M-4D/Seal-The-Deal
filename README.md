# TowerRace (working title)

A "friend slop" multiplayer tower-climbing race for 2–4 players, built in Unity 6.
Target: Steam Early Access, **Thu 29 Oct 2026** (hard deadline).

## Core game

- 2–4 players race up one tower.
- Players disrupt each other with knockback and disruption tools: blast gun,
  grapple, platform breaker. No kill weapons.
- Destruction uses pre-broken panels, host-authoritative.
- Short rounds, checkpoints or a height floor, catch-up for last place.

## Tech

- Unity 6.
- Host-authoritative P2P over Steam. No dedicated servers.
- Netcode: FishNet or Netcode for GameObjects + Steam transport. **Decision pending; see below.**

## Schedule

| Week | Ends | Goals |
|---|---|---|
| 1 | Thu 1 Oct | Networked movement, climbing and synced knockback in a greybox. Lock a simple art style. |
| 2 | ~Wed 7 Oct | Tower, 3 tools, destructible panels. Store page screenshots. **Submit store page ~7 Oct.** |
| 3 | ~Wed 14 Oct | Round loop, catch-up, 4-player playtests. |
| 4 | ~Tue 20 Oct | Steam lobbies and invites, polish. **Submit build ~20 Oct.** |
| 5 | Thu 29 Oct | Fixes, release. |

## Out of scope for launch

Multiple towers, cosmetics, matchmaking, progression.

## First steps

1. Choose the netcode stack.
2. Create the Unity 6 project in this folder.
3. Get a 2-player Steam lobby working in a greybox scene.

## Repo notes

- Large binary assets go through Git LFS (see `.gitattributes`). Run `git lfs install` once per machine.
- Unity should use **Visible Meta Files** and **Force Text** asset serialization (the Unity 6 defaults).
