# Close the Deal — Claude Code context

**Read [AGENTS.md](AGENTS.md) first.** It is the single entry point for every agent and tool: repo rules, project map (§10), compile check (§11) and the shared notes (§12). Where it and a harness default disagree, AGENTS.md wins. Don't restate its rules here.

Then check [docs/GDD.md](docs/GDD.md) for scope, [docs/notes/INDEX.md](docs/notes/INDEX.md) for shared notes, and [docs/WORKBOARD.md](docs/WORKBOARD.md) for who is doing what.

## Claude-only: memory

Shared notes live in [docs/notes/](docs/notes/) (AGENTS.md §12), so other people's agents see them too. That copy is the record.
The harness also reads a per-user mirror at `~/.claude/projects/C--Users-xyada-Documents-GitHub-TowerRace/memory/` for recall at session start. If the folder is renamed, that path changes with it.

When writing or updating a memory about this project:

1. Write it to `docs/notes/` and update its line in `docs/notes/INDEX.md`.
2. Copy the file and index line into the harness mirror (its index is `MEMORY.md`).
3. Commit the `docs/notes/` change.

Deleting means removing it from both. If the two disagree, the repo copy wins.
Memories about one person's own preferences that don't concern the project stay in that person's private config, not here.
