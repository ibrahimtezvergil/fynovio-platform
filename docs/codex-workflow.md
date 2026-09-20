# Codex workflow

This is the Codex supplement to the binding [AGENTS.md](../AGENTS.md) contract
and the shared working method in [CLAUDE.md](../CLAUDE.md); it intentionally
does not duplicate either document.

- Start Codex from the assigned worktree. Project-local `.codex/` configuration,
  hooks, and rules require project trust; do not change the user-level config
  from this repository.
- Follow CLAUDE.md's retrieval hierarchy. Prefer native Codex review with
  `codex review --base main` for a completed non-trivial branch; it complements
  the independent cross-model review flow in AGENTS.md.
- The safety hook hard-blocks its destructive-command patterns. Codex 0.155.1
  cannot implement Claude's PreToolUse `ask` result, so an exception requires
  an explicitly reviewed manual workflow rather than a silent fallback.
- `safety.rules` are defense in depth for sandbox escalations. Verify a changed
  rule with `codex execpolicy check --rules .codex/rules/safety.rules -- <cmd>`.
- In a linked worktree, the installed Graphify post-commit hook exits by design.
  Run `graphify update .` before committing code, then use AGENTS.md's required
  status and graph-artifact commit procedure.
- Do not add MCP servers, custom skills, persistent memory, compaction hooks,
  or automatic subagents without a task-specific, measurable need.
