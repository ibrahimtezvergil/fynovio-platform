# CLAUDE-BOOTSTRAP.md

Version: 2.0
Purpose: provision the **minimum sufficient** Claude Code environment for *this* repository.

This is a provisioning spec, not persistent task context. Do **not** import it from `CLAUDE.md`. Do not tell normal sessions to re-read it.

It is language-, framework-, runtime-, and topology-agnostic. Detect the repository first; never assume a stack.

---

## 0. Execution contract

Non-negotiable. Violating any line below is a failed run.

**Safety**

- Start in plan mode. Present the complete proposed change set before writing any file.
- Never run destructive git: `reset --hard`, `clean -fd`, force push, history rewrite.
- Never commit, never create branches, never stage files unless explicitly told to in this run's prompt.
- Never write to `.env*`, credential files, private keys, or production config. Never print secret values.
- Never modify production runtime behavior, business logic, public APIs, DB schemas, deployment config, or CI that runs on shared infrastructure.

**Dependencies**

- Default: **do not install anything.** Do not touch lockfiles.
- Dev-tooling installs are `RECOMMENDATIONS` unless this run's prompt explicitly authorizes installation.
- If authorized: use the repo's existing dependency manager, respect the lockfile, install one thing at a time, and inspect the resulting diff. If unrelated churn appears, revert and downgrade to a recommendation.

**Budget** (hard caps — stop and report when hit, do not silently continue)

- ≤ 40 files read during reconnaissance.
- ≤ 6 created/modified artifacts total.
- ≤ 200 lines in root `CLAUDE.md`.
- No file read solely "for completeness".

**Output**

- Final report written to `docs/claude/BOOTSTRAP-REPORT.md`.
- Ownership manifest written to `.claude/.bootstrap-manifest.json`.

---

## 1. Prime directive

Do not configure everything Claude Code supports. Do not install every tool considered best practice.

For every persistent mechanism, answer before creating it:

- What recurring problem does this solve?
- What repository evidence proves the problem exists?
- Does equivalent functionality already exist (in the repo, or natively in Claude Code)?
- Is there a smaller native mechanism that solves it?
- Does recurring benefit clearly exceed context cost + maintenance cost?

If the answer is not clearly positive: **do not add it.**

A minimal result is a valid result. For many repositories the optimum is `CLAUDE.md` alone.

---

## 2. Use native mechanisms before building anything

Claude Code already ships most of what a bootstrap is tempted to reinvent. Check these **first**, and prefer them.

| Need | Native mechanism | Notes |
| --- | --- | --- |
| Generate a starting `CLAUDE.md` | `/init` | With `CLAUDE_CODE_NEW_INIT=1`: multi-phase, explores via subagent, asks follow-ups, presents a reviewable proposal before writing. If `CLAUDE.md` exists, `/init` suggests improvements instead of overwriting. |
| Import another agent's config | `/import`, or `@AGENTS.md` in `CLAUDE.md` | Claude Code reads `CLAUDE.md`, not `AGENTS.md`. `/init` also reads `.cursor/rules/`, `.cursorrules`, `.github/copilot-instructions.md`. |
| `CLAUDE.md` is too big | `/doctor` | Proposes trims: cuts what Claude can derive from the codebase, keeps pitfalls and non-default conventions. |
| Verify what actually loaded | `/context` → **Memory files** | This is the real validation step. A file that doesn't appear here is not loaded. |
| Debug *why* an instruction loaded | `InstructionsLoaded` hook | Logs which instruction files load, when, and why. Useful for path-scoped rules. |
| Scope instructions to paths | `.claude/rules/*.md` with `paths:` frontmatter | Loads only when Claude touches matching files. Rules without `paths` load every session at `.claude/CLAUDE.md` priority. |
| Ignore other teams' `CLAUDE.md` in a monorepo | `claudeMdExcludes` setting | Glob against absolute paths; put it in `settings.local.json`. |
| On-demand procedure | Skill (`.claude/skills/<name>/SKILL.md`) | Descriptions load at session start; body loads on use. |
| Skill with side effects | `disable-model-invocation: true` | Zero context cost until manually invoked. |
| Isolate high-volume work | Subagent; `context: fork` on a skill | Built-in `Explore` / `Plan` omit `CLAUDE.md` and git status by design. |
| Large fan-out / cross-checked findings | Dynamic workflows | Beyond a handful of subagents. |
| Cross-agent coordination | Agent teams | Advanced; only when workers must talk to each other, not just return results. |
| Hard enforcement | `PreToolUse` hook, or `permissions.deny` | `CLAUDE.md` and rules are context, **not** enforced configuration. A rule that must always hold is a hook or a deny rule. |
| Claude accumulating its own learnings | Auto memory (on by default) | Machine-local, per-repo, `~/.claude/projects/<project>/memory/`. Not shared via git. Never the sole home of a critical invariant. |

Rule of thumb: `CLAUDE.md` = always-on facts · `.claude/rules/` = path-scoped facts · Skill = procedure · Hook = enforcement · Subagent = context isolation.

**Do not depend on any of the above without verifying the installed version supports it.** Claude Code changes fast. Check with `claude --version`, `claude --help`, and the relevant help surface. If the installed version differs from this document, use the currently supported equivalent. Never generate obsolete or invalid configuration.

---

## 3. Execution sequence

Run in order. Phases A–C write nothing.

### Phase A — Capture state (no writes)

1. `git status`. If not a git repository, note it: worktrees, auto-memory scoping, and reversibility guidance all change. Do not `git init`.
2. Inventory existing Claude configuration without reading everything in full:
   `CLAUDE.md`, `.claude/CLAUDE.md`, `CLAUDE.local.md`, `.claude/rules/`, `.claude/settings.json`, `.claude/settings.local.json`, `.claude/agents/`, `.claude/skills/`, `.claude/commands/`, `.mcp.json`, installed plugins, existing hooks.
3. Check for `.claude/.bootstrap-manifest.json`. If present, this is a **re-run** — see §9.
4. Detect the installed Claude Code version and which mechanisms in §2 it actually supports.

### Phase B — Reconnaissance (no writes, ≤ 40 files)

Answer *"how should Claude work here?"* — not *"how does every feature work?"*

High-signal surfaces only:

- repository root and top-level structure
- README and maintained architecture docs
- dependency/package manifests, workspace/monorepo manifests
- build files, task runners, compiler config
- test config, lint / format / static-analysis / type-check config
- CI definitions
- container / dev-environment config

Evidence precedence when sources conflict:

1. executable repository configuration (manifests, task scripts, build config, CI, test config)
2. existing repository-specific Claude instructions
3. maintained engineering documentation
4. README
5. repeated repository conventions
6. targeted source inspection
7. inference — never promote inference to a permanent rule

Never conclude a technology is absent because one particular manifest is missing. Expand only when an actual configuration decision depends on missing information.

Extract, from evidence:

- primary/secondary languages, frameworks, runtimes, dependency managers
- repository topology: workspaces, packages, services, deployable units, ownership boundaries
- **canonical commands**, verbatim from where they are defined: install, dev, build, targeted test, package test, full test, lint, format, static analysis, type check, CI-equivalent verification
- verification capability, as YES / PARTIAL / NO: targeted tests runnable? tests trustworthy? local verification approximates CI? static analysis or type checking present? formatting/linting present? changes locally reversible? high-risk data/deployment operations present?
- architecture invariants worth stating once (e.g. "external integrations pass through the adapter boundary")
- high-risk areas: auth, multi-tenancy, billing, migrations, concurrency, public contracts
- sensitive paths that actually exist in this repo (do not guess generic paths)

Do not learn the business domain. Bootstrap discovery answers how Claude should *work*; task discovery answers how a feature works.

### Phase C — Decide, then gate

For each candidate artifact, state in one line: `PROBLEM → EVIDENCE → SMALLEST NATIVE MECHANISM → ROI`.

Drop anything that fails. Then **present the full proposed change set and stop for approval.** Include:

- every file to be created or modified, with its content or diff
- every permission rule, with its exact scope
- any tooling install, flagged as `REQUIRES AUTHORIZATION`
- what you deliberately chose *not* to add, and why

Do not proceed past this gate without approval.

### Phase D — Apply

Write only what was approved. Order: `CLAUDE.md` → rules → settings/permissions → skills → agents → hooks → MCP. Stamp every generated artifact per §9.

### Phase E — Validate

- `/context` → confirm intended files appear under **Memory files**. Missing means not loaded.
- Parse-check every JSON artifact.
- Confirm every command referenced in `CLAUDE.md` exists where you claimed it does.
- Confirm skill and agent frontmatter is valid for the installed version.
- Confirm no rule contradicts another rule or `CLAUDE.md`.
- If dependencies were installed: inspect the lockfile diff.
- Inspect the final `git diff` in full.
- Do not launch autonomous Claude sessions to "test" the setup. Configuration-level validation is sufficient.

### Phase F — Report

Write `docs/claude/BOOTSTRAP-REPORT.md` (§10) and `.claude/.bootstrap-manifest.json` (§9). Then run the ROI pass: for every artifact created, ask *"could this be removed with nearly the same effectiveness?"* If yes, remove it.

Stop. Do not keep adding infrastructure to make the setup look sophisticated.

---

## 4. CLAUDE.md

Persistent high-signal project context. Not a handbook.

Target **under 200 lines**. It loads into every request; longer files consume context and measurably reduce adherence. Files over 4 MiB are skipped entirely.

It answers only:

- What would Claude otherwise rediscover every session?
- What mistake would Claude repeatedly make without this rule?
- What architecture invariant must always be known?
- Which commands are canonical?

Useful sections: Repository Map · Canonical Commands · Architecture Invariants · Editing Rules · Verification Rules · High-Risk Areas.

Write instructions concrete enough to verify. `Run \`pnpm test:unit\` before committing` — not `Test your changes`. `API handlers live in src/api/handlers/` — not `Keep files organized`.

Multi-step procedures do not belong here. They belong in Skills.

**Imports.** `@path/to/file` expands into context at launch — it organizes, it does not save context. Add an import only if Claude needs that content in nearly every session. Wrap a path in backticks to mention it without importing. Max depth 4 hops. Imports resolving outside the working directory trigger an approval dialog.

**If the repo already has `AGENTS.md`:** create `CLAUDE.md` containing `@AGENTS.md` plus any Claude-specific additions, rather than duplicating content. A symlink also works on non-Windows.

**Notes for human maintainers** go in block-level HTML comments — they are stripped before entering context.

---

## 5. `.claude/rules/`

Use when instructions are area-scoped or would otherwise bloat root `CLAUDE.md`.

- One topic per file, descriptive filename. Discovered recursively.
- Without `paths:` frontmatter, a rule loads every session at `.claude/CLAUDE.md` priority — it is not free.
- With `paths:` frontmatter, it loads only when Claude touches matching files. This is the context-saving case and usually the reason to use rules at all.

```markdown
---
paths:
  - "src/api/**/*.ts"
---

# API rules
- All endpoints validate input at the boundary.
- Errors use the standard response envelope.
```

Good candidates: subtree architecture constraints · language-specific rules in a polyglot repo · test conventions · migration/schema constraints.

Do not create many tiny rule files. Each durable rule has exactly one authoritative home. In a monorepo, prefer path-scoped rules over nested `CLAUDE.md` files; use nested `CLAUDE.md` only where a subtree is genuinely a distinct development context with its own commands and workflow.

---

## 6. Skills, agents, hooks

**Skills.** Prefer `.claude/skills/<name>/SKILL.md` for recurring procedures. Justified only when: the procedure recurs **and** repository-specific steps matter **and** encoding it reduces future prompting, errors, or supervision. If ordinary Claude behavior suffices, create nothing. Preserve existing `.claude/commands/`; migrate only for real functional value, never for aesthetics.

**Agents.** Prefer built-ins. Do not create a custom explorer to duplicate `Explore`. Create a custom agent only for durable specialization: tool restrictions, preloaded skills, repository-specific procedure, persistent memory, a stable domain role. Note that `Explore` and `Plan` deliberately omit `CLAUDE.md` and git status — do not rely on them knowing project rules; put what they need in the prompt or in preloaded skills. Do not copy the repository handbook into every agent.

Enable subagent persistent memory only when accumulated cross-task knowledge materially improves future runs and cannot go stale misleadingly. Critical invariants live in shared instructions, never only in agent memory.

Handoff format — compressed evidence, not narrative or raw logs:

```
FILES      path::symbol
FINDINGS   ...
CONSTRAINTS ...
TESTS      path::test_name
UNKNOWNS   ...
```

```
CHANGED    ...
VERIFIED   command → result
RISKS      ...
```

**Hooks.** Default: **none.** Create one only where deterministic enforcement beats prompting — because `CLAUDE.md` and rules are context, not enforcement. Good: protected-path blocking, cheap format/syntax validation, generated-artifact validation, dangerous-command rejection. Bad: full test suite after every edit, network calls, large logs, fragile machine-specific automation. Prefer deterministic command hooks over prompt/model hooks; use model hooks only where real judgment is needed. Never create recursive `Stop`-hook loops. Machine-specific hooks stay in local settings, never shared settings.

---

## 7. Settings, permissions, sensitive paths

- `.claude/settings.json` — team-shared behavior only.
- `.claude/settings.local.json` — machine/user overrides. Ensure it is gitignored.
- `~/.claude/settings.json` — personal defaults. Never push personal preferences (model choice, UI, personal integrations, machine paths) into shared settings.
- `CLAUDE.local.md` — personal project notes. Ensure it is gitignored.

**Permissions.** Zero friction ≠ broad permissions. Allow the narrowest durable rules that remove genuinely repetitive low-risk prompts — the exact test, lint, format, type-check, and read-only commands you verified in Phase B. Never broadly allow arbitrary shell, destructive git, arbitrary network, deployment, production, or secret access. Never use a permission-bypass mode as the default zero-friction strategy. Goal: few unnecessary prompts **plus** strong deterministic boundaries.

**Sensitive paths.** Add `permissions.deny` entries only for sensitive paths you actually found in Phase B. Never weaken an existing deny rule.

---

## 8. MCP, plugins, tooling boundaries

**Preserve** existing MCP servers and plugins that work.

**Auto-configure** a repository-local MCP only when: no secret, no paid service, no privileged account, repo-local, reversible, clear recurring value.

**Recommend, never auto-provision**, anything requiring OAuth/login, credentials, external accounts, privileged network access, paid services, or org permissions. Use environment-variable references, never embedded secrets.

**MCP context.** Do not raise output limits as the first response to oversized output. Prefer pagination, filtering, narrower queries, specific resources. Raise limits only when large responses are genuinely required and recurring.

**Plugins.** Auto-install only if the source is already trusted by repository policy **and** it is development-only **and** no secrets or privileged access are introduced **and** recurring value is clear. Otherwise recommend. Prefer plain `.claude/` configuration; use a plugin only when skills + agents + hooks + MCP must travel together and reuse is genuinely useful.

**Automatic authority never extends to:** production dependencies or infrastructure, databases, deployment platforms, cloud resources, paid services, external accounts, org-wide tooling, credentials, irreversible external state. Report these as recommendations.

**Model/effort.** Do not hard-code current model names into shared repository configuration. Describe capability instead: exploration → fast/cheap; routine implementation → capable coding model; ambiguous debugging → stronger reasoning; high-risk refactor → high-capability reasoning. Concrete model and cost preferences are personal, not shared.

**Helper scripts.** Only when they eliminate a repeated complex command or make autonomous validation materially safer. Small, deterministic, repository-specific. Never a parallel build system for Claude, never a wrapper around a one-line command.

---

## 9. Idempotency and ownership

This bootstrap must be safely re-runnable. That requires knowing what it owns.

**Stamp every generated artifact.** Files with frontmatter (rules, skills, agents) get:

```yaml
generated-by: claude-bootstrap
bootstrap-version: 2.0
```

Sections written into a file that also holds human content (`CLAUDE.md`, existing settings) get managed markers:

```markdown
<!-- claude-bootstrap:begin -->
...generated content...
<!-- claude-bootstrap:end -->
```

Content outside the markers is human-owned and is never touched.

**Write `.claude/.bootstrap-manifest.json`:**

```json
{
  "version": "2.0",
  "generatedAt": "<ISO 8601>",
  "claudeCodeVersion": "<detected>",
  "artifacts": [
    { "path": ".claude/rules/testing.md", "mode": "owned", "sha256": "..." },
    { "path": "CLAUDE.md", "mode": "managed-block", "sha256": "..." }
  ],
  "recommendations": ["..."],
  "omitted": [{ "mechanism": "hooks", "reason": "no recurring supervision cost found" }]
}
```

**On re-run:** inspect → compare hashes → preserve anything modified by a human (report the drift, do not overwrite) → repair stale bootstrap-owned artifacts → add newly justified capabilities → remove bootstrap-owned artifacts that are now clearly obsolete → validate.

Never duplicate rules, skills, agents, or hooks. Never create a second mechanism for a job an existing one already does. Never rewrite functioning configuration merely because a different layout exists.

Without a manifest entry or a marker, an artifact is **not** bootstrap-owned and must not be deleted.

---

## 10. Final report

Write to `docs/claude/BOOTSTRAP-REPORT.md`. Concise, not a tutorial.

```
REPOSITORY          detected stack / topology
CLAUDE CODE         detected version / mechanisms available
EXISTING TOOLCHAIN  canonical commands, verification capability
CREATED             path → why
UPDATED             path → what changed
CLAUDE CONTEXT      CLAUDE.md and rules decisions
SKILLS              added / preserved / omitted
AGENTS              built-in vs custom strategy
PERMISSIONS         friction reductions and safety boundaries
HOOKS               added, or intentionally omitted
MCP / PLUGINS       preserved / added / recommended
PRESERVED           existing configuration left intact
NOT ADDED           mechanisms deliberately omitted, and why
VERIFIED            command or check → result
RECOMMENDATIONS     valuable changes outside bootstrap authority
LIMITATIONS         environment or version constraints
FINAL STATE         why this is the minimum sufficient setup
```

The `NOT ADDED` section is mandatory and is the most useful part of the report.

---

## 11. Working conventions this bootstrap should encode

Only encode these if repository evidence supports them.

**Verification depth** — never run the full suite after every edit:

- small/local change → cheapest meaningful check
- module behavior change → targeted tests + relevant static checks
- cross-cutting or risky change → targeted checks + broader integration verification
- release-sensitive change → CI-equivalent verification when practical

**Testing** — do not force TDD. Behavioral bug → regression test first when practical. Feature with a clear contract → test first when valuable. Mechanical refactor → existing tests. Config/docs → no artificial tests. Never delete tests to get green, weaken assertions without cause, disable checks to bypass failure, hard-code behavior to satisfy visible tests, or hide failures instead of finding root cause.

**CI repair** — reproduce → diagnose root cause → minimum coherent patch → focused verification → broader verification when justified. Never via test deletion, assertion weakening, validation disabling, unrelated dependency upgrades, or broad refactors. Do not modify production CI to invoke Claude Code.

**Context economy** — hand off coordinates (`path::symbol`, `path::test_name`), not file dumps. Do not rediscover what exploration already found. Surface the relevant failure, assertion, and stack frames — not full logs, full diffs, or repository-wide search dumps.

**Delegation** — delegate only when it reduces total context, work, latency, or risk. Small task → main conversation. Search-heavy investigation → `Explore`. Independent slices → parallel agents with disjoint file ownership. Overlapping writers → worktrees, created at task time, never during bootstrap. More agents means more tokens; maximize useful throughput, not concurrency.

**Review** — risk-driven, not automatic. Warranted for security, auth, multi-tenancy, billing, migrations, public contracts, concurrency, large refactors, data integrity. Priorities: P0 security/data loss · P1 correctness/regression · P2 architecture/performance · P3 maintainability. Never invent findings to look productive.

**Out-of-scope findings** — finding engineering problems is allowed; silently redesigning them is not. Classify as `BLOCKING` / `HIGH VALUE` / `OPTIONAL` and report.

---

## 12. Success criteria

The run succeeded when:

- no stack was assumed before discovery
- native mechanisms were preferred over custom duplication
- `CLAUDE.md` is concise, evidence-based, and under 200 lines
- procedures live in Skills, not in persistent context
- scoped rules are used only where they save context or prevent real mistakes
- permission friction was reduced by narrow rules, not broad bypasses
- sensitive paths stay protected and no secrets were exposed
- no production, credentialed, paid, or irreversible external state was touched
- every artifact is stamped and recorded in the manifest
- the setup is valid, validated, and safely re-runnable
- configuration complexity is lower than the recurring friction it removes

The goal is not the most elaborate configuration. It is the smallest environment in which Claude Code can work as autonomously, cheaply, and reliably as this specific repository safely allows.
