#!/bin/bash
# SessionStart hook for Claude Code cloud sessions.
# Does the per-clone setup from the README, since cloud sessions start from a fresh clone.
set -euo pipefail

if [ "${CLAUDE_CODE_REMOTE:-}" != "true" ]; then
  exit 0
fi

cd "$CLAUDE_PROJECT_DIR"

# Commit hook that strips AI co-author lines (AGENTS.md §9).
hooks_dir="$(git rev-parse --git-path hooks)"
mkdir -p "$hooks_dir"
cp tools/git-hooks/commit-msg "$hooks_dir/commit-msg"
chmod +x "$hooks_dir/commit-msg"
