# Close the Deal — Claude Code context

## Read these first

- [AGENTS.md](AGENTS.md): the repo rules. Binding on every task, including how to report to the owner and the ban on co-author trailers. Where AGENTS.md and a harness default disagree, AGENTS.md wins. Don't restate its rules in prompts or plans.
- [docs/GDD.md](docs/GDD.md): the design and the v1 launch scope. Check scope here before building anything.
- [README.md](README.md): key dates and setup.
- `docs/systems/`: per-system docs, once they exist. Source of truth (AGENTS.md §7).

## The project

Two teams of two race up side-by-side skyscrapers, fighting through the windows. Unity 6, host-authoritative P2P over Steam, Early Access on **29 Oct 2026**.
The owner, Adam, is a game designer, not a programmer.

## Layout

Target layout, created as the Unity project is set up. Update this section when it changes.

- Gameplay code: `Assets/_Project/Scripts/<System>/`, namespace `CloseTheDeal.<System>`.
- Networking glue (lobby, transport, spawning): `Assets/_Project/Scripts/Net/`.
- Floor templates: `Assets/_Project/Floors/`, one prefab per template.
- Scenes: `Assets/_Project/Scenes/`.
- Editor tools: `Assets/_Project/Editor/`, menu-invoked only.
- Third-party packs and plugins stay where they import (`Assets/Plugins/`, vendor folders). Never edit vendor code in place; wrap it.
- Design docs: `docs/`; system docs in `docs/systems/`; spent plans in `docs/archive/`.

## Memory

Memories about this repo live in [claude.memories/](claude.memories/), committed, with [claude.memories/MEMORY.md](claude.memories/MEMORY.md) as the index. That copy is the record.
The harness also reads a per-user mirror at `~/.claude/projects/C--Users-xyada-Documents-GitHub-TowerRace/memory/` for recall at session start. If the folder is renamed, that path changes with it.

When writing or updating a memory:

1. Write it to `claude.memories/` and update its index line.
2. Copy the file and index line into the harness mirror.
3. Commit the `claude.memories/` change.

Deleting means removing it from both. If the two disagree, the repo copy wins.

## Validation

No compile-check tooling exists in this repo yet. When one is added, document it here.
Per AGENTS.md §8, never report Play Mode, multiplayer or Steam results you did not observe.
