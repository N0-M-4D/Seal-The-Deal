# Close the Deal (working title)

Two rival companies race up two skyscrapers side by side, shooting at each other
through the windows. The first team to reach the roof wins.

- **Genre:** co-op/PvP friend-slop, 2v2 at launch, short replayable runs.
- **Engine:** Unity 6, host-authoritative peer-to-peer over Steam. No dedicated servers.
- **Release:** Steam Early Access, **Thu 29 Oct 2026**.

The full design is in [docs/GDD.md](docs/GDD.md).

## Key dates

| Date | Milestone |
|---|---|
| 7 Oct | **Submit store page.** Cannot slip: it starts the 14-day Coming Soon clock. |
| ~20 Oct | Submit build for review. |
| 29 Oct | Early Access release. |

Steam rules: no release until 30 days after the app fee is paid; Coming Soon must
be live for 14+ days; allow 7 days for each review.

## First steps

1. ~~Choose the netcode stack.~~ FishNet + FishySteamworks (see the GDD).
2. ~~Create the Unity 6 project in this folder.~~ Done: Unity 6000.4.8f1, URP.
3. ~~Get a 2-player Steam lobby working in a greybox scene.~~ Built; see [docs/systems/NETCODE.md](docs/systems/NETCODE.md). Needs its first two-PC test.
4. ~~Host-authoritative movement, climbing and knockback in the greybox.~~ Built; see [docs/systems/PLAYER_MOVEMENT.md](docs/systems/PLAYER_MOVEMENT.md). Needs its first play.
5. ~~Two greybox towers from templates, built from a host-chosen seed.~~ Built; see [docs/systems/TOWER.md](docs/systems/TOWER.md). Needs its first play.
6. ~~Physics props, host-simulated.~~ Built; see [docs/systems/PROPS.md](docs/systems/PROPS.md). Ragdolls still to do. Needs its first play.
7. The three tools: rocket launcher, throwable, zipline gun, on the knockback system.

## Working on it

- Open the repo root in Unity **6000.4.8f1**.
- FishNet and Steamworks.NET come from GitHub through the Package Manager, so **git must be on your PATH** the first time Unity opens the project. Their versions are pinned in `Packages/manifest.json`. FishySteamworks is committed under `Assets/FishNet/Plugins/FishySteamworks/` (it cannot be a Package Manager package).
- Steam features need the **Steam client running**. Until we have our own app id, testing uses Valve's public test app: put a `steam_appid.txt` containing `480` in the repo root (it is git-ignored and must never ship).
- Testing two players on one PC needs two Steam accounts, or a second PC.

## Repo notes

- Repo rules for people and agents: [AGENTS.md](AGENTS.md).
- Large binary assets go through Git LFS (see `.gitattributes`). Run `git lfs install` once per machine.
- After cloning, install the commit hook that strips AI co-author lines: `cp tools/git-hooks/commit-msg .git/hooks/`.
- Unity should use **Visible Meta Files** and **Force Text** asset serialization (the Unity 6 defaults).
