# fynovio-platform — Claude Code Notes

Engineering rules (stack, code conventions, architecture invariants, database rules, testing, generated-artifact policy) live in **`AGENTS.md`** — read that first; it's the model-independent contract and applies whether the agent is Claude Code, Codex, or a human. This file covers only Claude-specific tool routing and methodology.

## Repository retrieval hierarchy
Investigate in this order; don't start with a repo-wide grep or reading whole directories:
1. This file + `AGENTS.md` + the relevant doc under `docs/`.
2. `graphify query "<question>"` / `graphify explain "<symbol>"` / `graphify path "<A>" "<B>"` (see graphify section below) once `graphify-out/graph.json` exists.
3. Targeted `rg` — the right tool for literals, config, generated output, exact error text.
4. A full file read, once the relevant class/function/region is identified.
5. A broad repository scan — last resort only, when the above didn't surface enough.

## Methodology routing
- **Trivial edit** (typo, formatting, a known one-liner) → just make the change, no ceremony.
- **Bug / test failure / unexpected behavior** → use the `systematic-debugging` skill before proposing a fix; find the root cause first.
- **Before claiming anything complete, fixed, or passing** → use `verification-before-completion`: run the actual command, show the actual output, don't assert without evidence.
- **Finishing a non-trivial implementation** → use `requesting-code-review` (dispatches the `code-reviewer` subagent with a scoped diff, not this session's history) before treating the task as done.
- **Receiving review feedback** (from Ibrahim or another agent) → use `receiving-code-review`: verify against the actual code before implementing, no performative agreement, push back with technical reasoning when the feedback doesn't hold up.

These are global skills already installed under `~/.claude/skills/` (adapted independently of the full Superpowers plugin, which is not installed) — nothing repo-local to set up for them.

## Reasoning effort
Default to normal effort. Escalate for: RLS/tenant-isolation code, EF Core migrations that touch money or the state-machine invariants, cross-module boundary decisions, anything that changes a file under `docs/architecture-analysis/` or `docs/schema/`. Don't stay pinned at maximum effort for routine edits — it's not free and doesn't help a typo fix.

## Output style
No ceremonial narration ("I'll now inspect...", "Let me check..."). State findings and changes directly. Keep completion notes to what changed and what's next — no long recap for a simple task.

## Plan checkbox tracking (`docs/plans/*.md`)
A step's checkbox (`- [ ]` → `- [x]`) is marked done **only after the corresponding commit exists** — never ahead of actual state. The step that contains the `git commit` gets a line directly under it: `→ Commit: \`<short-hash>\` "<commit message>"`. If a follow-up fix commit corrected that step's work (a subagent slip, a review finding), note it in the same line: `(follow-up fix: \`<hash>\` "<message>")`. If something about the step deviated from what the plan originally said, add a short note explaining why. This applies to every plan written from here on; see `docs/plans/2026-09-16-pilot-enforcement.md` for the worked example.

## Safety
A repo-level PreToolUse guard (`.claude/hooks/guard.py`, registered in `.claude/settings.json`) blocks or asks-first on destructive Bash patterns (`rm -rf`, force-push/`reset --hard`/`clean -f` on `main`/`master`, `DROP DATABASE`/`DROP TABLE`/`TRUNCATE`, `dotnet ef database drop`, `terraform destroy`, etc.) as defense-in-depth on top of Claude Code's native sandbox and permission settings — it does not replace them. Never make a permanent-skip-permissions mode part of the normal workflow.

## Memory
Project state, past architecture decisions, and open questions are tracked in Claude Code's native Auto Memory (project-scoped; not committed to this repo). No second persistent memory system is in use here — don't introduce one without first showing native memory is insufficient.

## graphify

This project has a knowledge graph at `graphify-out/` with god nodes, community structure, and cross-file relationships. `graph.json`, `GRAPH_REPORT.md`, and `manifest.json` are committed; `graphify-out/cache/` and `graph.html` are gitignored and regenerable.

Rules:
- For codebase questions, first run `graphify query "<question>"` when `graphify-out/graph.json` exists. Use `graphify path "<A>" "<B>"` for relationships and `graphify explain "<concept>"` for focused concepts. These return a scoped subgraph, usually much smaller than `GRAPH_REPORT.md` or raw grep output.
- If `graphify-out/wiki/index.md` exists, use it for broad navigation instead of raw source browsing.
- Read `graphify-out/GRAPH_REPORT.md` only for broad architecture review or when query/path/explain don't surface enough context.
- After modifying code, run `graphify update .` to keep the graph current (AST-only, no API cost).
