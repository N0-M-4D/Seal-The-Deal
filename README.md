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

## Playtesting with a friend (first time)

1. Accept the GitHub invite, then clone the repo with **GitHub Desktop** (it handles Git LFS).
2. Install **Unity 6000.4.8f1** from Unity Hub and open the cloned folder. The first open takes a while: it downloads the netcode packages from GitHub.
3. Have **Steam running and signed in**, and be **Steam friends** with whoever is hosting.
4. Open `Assets/_Project/Scenes/Greybox.unity` and press **Play**.
5. **Host:** press **Host game**, then **Copy**, and send the code to your friend.
   **Friend:** paste the code under **Join with a lobby code** and press **Join**.
6. Play. **Esc** brings the menu back; the controls are listed on it.

Both players must be on the same commit: pull before you play.
If something breaks, note what you did, what you expected, and copy anything red from Unity's Console.

## Working on it

- Open the repo root in Unity **6000.4.8f1**.
- FishNet and Steamworks.NET come from GitHub through the Package Manager, so **git must be on your PATH** the first time Unity opens the project. Their versions are pinned in `Packages/manifest.json`. FishySteamworks is committed under `Assets/FishNet/Plugins/FishySteamworks/` (it cannot be a Package Manager package).
- Steam features need the **Steam client running**. Until we have our own app id, testing uses Valve's public test app (480); Steamworks.NET writes `steam_appid.txt` into the repo root on first open (it is git-ignored and must never ship).
- Without Steam the game runs in **local test mode**: an editor and a build on one PC can play together. See [docs/systems/NETCODE.md](docs/systems/NETCODE.md).

## Repo notes

- Repo rules for people and agents: [AGENTS.md](AGENTS.md). Every AI tool reads it first; shared notes, decisions and the workboard live in `docs/` (see its §12).
- Large binary assets go through Git LFS (see `.gitattributes`). Run `git lfs install` once per machine.
- After cloning, install the commit hook that strips AI co-author lines: `cp tools/git-hooks/commit-msg .git/hooks/`.
- Unity should use **Visible Meta Files** and **Force Text** asset serialization (the Unity 6 defaults).
