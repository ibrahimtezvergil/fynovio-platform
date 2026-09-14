# AI tooling — architecture record

Concise record of AI development-environment decisions for this repo, audited and applied 2026-09-14. Optimization target: tokens per verified successful task, not tool count. Full rationale for each classification lives in the setup session; this is the stable summary to keep current, not a copy of that session.

## CORE — always active
- **Claude Code native safety** — sandbox + `.claude/settings.json` permissions (`deny` list for `rm -rf`, force-push, `reset --hard`, `clean -f`, `DROP TABLE/DATABASE/SCHEMA`, `TRUNCATE`).
- **Repo PreToolUse guard** (`.claude/hooks/guard.py`) — defense-in-depth on top of the above; travels with the repo regardless of whose personal Claude config is active. Verified against synthetic PreToolUse input (2026-09-14): correctly denies `rm -rf` (including chained via `&&`), asks on force-push/`reset --hard`/destructive SQL/`dotnet ef database drop`/`terraform destroy`/reading files that look like secrets, stays silent on ordinary commands.
- **Claude Code native Auto Memory** — project-scoped, not committed to this repo.
- **Claude Code native compaction** — not disabled, no hard-coded context-window threshold.
- **Graphify** (v0.9.55, installed at `~/.local/bin/graphify`) — project-scoped Claude Code integration (`graphify install --project --platform claude`): `.claude/skills/graphify/`, PreToolUse hooks for search/read, a `## graphify` section in `CLAUDE.md`. Code graph built local-AST-only (`graphify extract . --code-only`, no LLM/API cost) — 366 nodes, 503 edges, 22 communities as of the first build. `graphify query`/`explain`/`path` verified working against the real graph. A git `post-commit`/`post-checkout` hook and merge driver are installed locally to keep the graph fresh automatically; `graph.json`/`GRAPH_REPORT.md`/`manifest.json` are committed (a merge driver exists specifically so this can be version-tracked), `cache/` and `graph.html` are gitignored and regenerable. Semantic/media ingestion (Mode B — LLM-assisted extraction over docs/PDFs/images) was **not** used and stays opt-in only; nothing in this repo triggers it.
- **`rg`** (14.1.1, already present) and **`fd`** (10.5.0, installed via Homebrew 2026-09-14) — targeted search infra.
- **`git`** (2.50.1) and **`gh`** (2.100.0, installed via Homebrew 2026-09-14, not yet authenticated — run `gh auth login` before using it).
- **Methodology skills** — `systematic-debugging`, `verification-before-completion`, `requesting-code-review`, `receiving-code-review` are already installed globally (`~/.claude/skills/`), independent of the full Superpowers plugin (confirmed not installed — `~/.claude/plugins/installed_plugins.json` is empty). No repo-local copy needed; see `CLAUDE.md`'s methodology-routing section for when each applies.

## LAZY / ON-DEMAND — present but not always active
- **UI UX Pro Max** — already installed globally as a skill (on-demand by nature; skills only load when invoked). No frontend code exists yet (`web/` is a placeholder); add a path-scoped `.claude/rules/frontend-ui.md` once real React source lands under `web/` or `src/**/*.tsx`, so backend-only work never pulls in UI guidance.
- **Archify, Context7** — not installed. Use Context7 when current library/framework docs are genuinely needed (version uncertainty, recently changed API); use Archify when a human-facing architecture diagram is actually requested. Neither is worth installing before that need is concrete.
- **Serena, Playwright MCP, GitHub MCP, Worktrunk** — not installed. Triggers: Serena when Graphify is insufficient for exact live references/rename-heavy refactors; Playwright MCP when persistent interactive browser state is genuinely needed (CLI/tests first, always); GitHub MCP only if `gh` genuinely can't express a needed workflow; Worktrunk only once 3+ sustained parallel agent sessions are a real pattern here (currently zero).

## EXPERIMENTAL — deferred, not installed
- **GSD (epic mode)**, **claude-mem**, **Headroom** — none installed. Re-evaluate only against measured deficiency (native Auto Memory/compaction proving insufficient, or a multi-phase migration large enough that native workflow demonstrably struggles) — not preemptively.

## REFERENCE ONLY — inspected, not adopted wholesale
- **Superpowers** (full plugin) — not installed; its three relevant skills are already adapted independently (see CORE).
- **Everything Claude Code**, **Awesome Claude Code** — discovery catalogs, not runtime infrastructure. Nothing adopted from them in this pass.

## OUTSIDE THE CODING STACK
- LightRAG, Obsidian Skills, Diagram Design, "I Have ADHD" — not relevant to this repo; not installed.

## Cross-agent
- **Codex CLI** is installed on this machine (`codex-cli 0.154.0`, `/opt/homebrew/bin/codex`) but was not configured or modified by this bootstrap. Cross-model review, when used, is invoked manually by the developer today — see `AGENTS.md`'s Cross-Agent / Multi-Agent Policy section for the intended flow once it's wired up.
- `AGENTS.md` at repo root is the model-independent contract (stack, code conventions, architecture/database rules, testing, generated-artifact policy, cross-agent policy). `CLAUDE.md` is Claude-specific only (retrieval hierarchy, methodology routing, reasoning-effort guidance, output style). Don't duplicate a rule into both.

## Explicitly not introduced
- **mise** — no existing `mise.toml`/`.tool-versions`/`.nvmrc`/`global.json`/etc. in this repo; per policy, not introduced just because a setup reference mentions it. The .NET SDK version is currently pinned only by each `.csproj`'s `<TargetFramework>net10.0</TargetFramework>`; a `global.json` pinning the exact SDK build is a reasonable future addition but is a separate, explicit decision, not part of this AI-tooling pass.
