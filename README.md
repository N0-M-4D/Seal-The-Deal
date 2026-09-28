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

1. Choose the netcode stack: FishNet or Netcode for GameObjects, with a Steam transport.
2. Create the Unity 6 project in this folder.
3. Get a 2-player Steam lobby working in a greybox scene.
4. Prove prop and ragdoll sync early. It's the biggest technical risk.

## Repo notes

- Large binary assets go through Git LFS (see `.gitattributes`). Run `git lfs install` once per machine.
- Unity should use **Visible Meta Files** and **Force Text** asset serialization (the Unity 6 defaults).
