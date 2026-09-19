# AI Development Stack — Decision Record (2026-09-07)

Source brief: `docs/claude/Claude_Code_Modern_Development_Stack_2026.pdf` ("Claude Code +
Codex Modern Development Stack | 2026"). That document targets an "Enterprise React
frontend + .NET 8 backend" environment; this repository is **frontend-only** (React 19 +
TypeScript SPA, Vite 7, no backend, no database, no queue). Every recommendation below
was re-derived from this repo's actual evidence, not copied from the PDF. Tools it names
are treated strictly as a *verified-source registry*, not an install list (see §12 of the
PDF itself).

## 0. Correction to the task premise

**Revised 2026-09-07, second pass.** The first pass of this review checked only
repo-local and Claude-project MCP/plugin configuration and concluded Graphify wasn't
installed. That was correct as far as it went but incomplete — it didn't check the
machine/user level. Re-checked against every scope named in the follow-up request:

| Scope checked | Result |
| --- | --- |
| Executable / `PATH` | **Found.** `/Users/ibrahimtezvergil/.local/bin/graphify` and `.../graphify-mcp`, both on `$PATH` (`.local/bin` is in it). `graphify --version` → `0.9.55`. |
| Global package/tool installation | **Found.** `uv tool` receipt at `~/.local/share/uv/tools/graphifyy/uv-receipt.toml`: package `graphifyy`, entrypoints `graphify` + `graphify-mcp`, installed **2026-09-07 10:06** (same day as this review, ~3.5h before the unrelated Claude Code bootstrap pass at 13:54). |
| Claude Code skills/plugins | **Not found.** `~/.claude/skills/` has 13 entries, none named graphify. `~/.claude/plugins/installed_plugins.json` does not contain the string "graphify". No graphify skill is registered for Claude Code on this machine. |
| Codex skills/config | **Not found.** `~/.codex/skills/` contains only `formshift` and OpenAI's built-in system skills (`skill-creator`, `plugin-creator`, `openai-docs`, etc.) — no graphify. `~/.codex/config.toml` not grepped as containing it either. |
| User-level configuration | **Not found beyond the uv receipt above.** No `~/.graphify`, no global graphify config file. |
| Repo-external agent configuration / any repo | **Not found.** No `graphify-out/` directory anywhere under `$HOME` or this repo; no `graph.json`/`GRAPH_REPORT.md` in this repo or elsewhere searched. |

**Corrected conclusion:** Graphify *is* installed on this machine (as a CLI + MCP
binary via `uv tool install graphifyy`), but it has never been **activated** for any
agent (`graphify install --platform claude|codex` was not run — that subcommand exists
per `graphify --help` and writes into exactly the skill directories checked above,
which are empty of it) and has never been **run against this repository** (no
`graphify-out/` artifacts exist anywhere). So: not "needs installing" — needs, at
minimum, one `graphify install --platform <agent>` per agent plus one `/graphify .`
run in this repo before it does anything for this codebase. That activation step was
**not performed** in this pass, per the explicit "no new installation" instruction —
see the revised §3.2 decision below.

## 1. Repository snapshot

| Axis | Finding |
| --- | --- |
| Stack | React 19.2, TypeScript ~6.0.2 (strict), Vite 7.3, Tailwind v4, shadcn/ui |
| Package manager | npm, `package-lock.json` committed |
| Node | v22.18.0 active; no `.nvmrc`/`.node-version`/`engines` existed before this change |
| Size | 346 `.ts`/`.tsx` files, ~35.3k lines, 18 features under `src/features/` |
| Backend | None. Data mocked via MSW behind React Query. |
| Lint | oxlint 1.79, with per-feature `no-restricted-imports` overrides enforcing feature isolation |
| Tests | vitest 5 + Testing Library; 2 test files, 26 tests (data-table URL state, filter query logic) |
| CI/CD | None existed (`.github/` absent) |
| Git | Single worktree, branch `ios-27`, remote `github.com/ibrahimtezvergil/fynovio-react` |
| Claude Code config | Root `CLAUDE.md` (managed-block, bootstrap-owned), `.claude/settings.json` (4 allow rules), `.claude/agents/test-verifier.md`. No rules/, skills/, hooks/, or MCP servers. |
| Codex config | Root `AGENTS.md` — confirmed via official docs that Codex CLI walks the project tree root-down looking for `AGENTS.override.md`/`AGENTS.md`, so this file is Codex's actual contract, not decoration. |
| Prior review | A Claude Code bootstrap pass ran earlier the same day (`docs/claude/.bootstrap-manifest.json`, `BOOTSTRAP-REPORT.md`) and reached largely the same minimal-footprint conclusion this review does — see §7. |

## 2. Validation loop run for this review

```
npm run lint      → exit 0, 37 pre-existing warnings (react/only-export-components,
                     one react/incompatible-library), no errors
npm test          → 2 test files, 26 tests, all passed
npm run build     → tsc -b (typecheck) + vite build, succeeded
                     (chunk-size warning >500kB on 2 vendor chunks — pre-existing,
                     out of scope for a tooling review)
npm run check     → lint + typecheck + test chained, verified working end-to-end
                     after this change (note: inherits lint's warning-tolerant exit
                     code — a green `check`/CI run can still carry the 37 warnings
                     above; see the AGENTS.md line added alongside this)
npm ci            → re-run after adding `engines`/scripts to package.json: it failed
                     against the stale lockfile until `package-lock.json` was
                     regenerated (`npm install --package-lock-only`) to match. Fixed
                     and re-verified clean (`rm` was not used — `npm ci` itself
                     replaces node_modules). This is the actual command CI runs, not
                     just `npm install`, and it would have failed on first push
                     without this fix.
```

No regression, no pre-existing failure was hiding behind an untested claim. The
`npm ci` desync above was caught precisely because the loop was re-run after editing
`package.json`, not assumed from the pre-edit run in this same section.

## 3. Need → Decision matrix

Format: **Need / Evidence / Current solution / Gap / Decision / Reason.**

### 3.1 Claude Code native (Skills, Hooks, Subagents, Worktrees) — **KEEP**
- Need: repo-standard AI workflow enforcement.
- Evidence: already in use — root `CLAUDE.md`, `.claude/settings.json`, one custom
  subagent (`test-verifier`).
- Current solution: exactly this, deliberately minimal per the prior bootstrap pass.
- Gap: none found for this repo's size/risk profile.
- Reason: adding rules/, skills/, or hooks/ now would duplicate global skills already
  available (`systematic-debugging`, `verification-before-completion`,
  `writing-plans`) or global agents (`code-reviewer`), for zero net benefit — this was
  already correctly assessed by the bootstrap pass and still holds.

### 3.2 Graphify (architecture knowledge graph) — **LATER (installed but inactive — activation is optional, your call)**
- Installation status (corrected — see §0): the `graphifyy` package **is installed on
  this machine** (`uv tool install graphifyy`, v0.9.55, entrypoints `graphify` and
  `graphify-mcp` on `$PATH`, installed 2026-09-07). It is **not activated** for either
  agent (no skill in `~/.claude/skills/` or `~/.codex/skills/`) and **has never been
  run against this repository** (no `graphify-out/` anywhere). This is a different
  situation from "candidate not yet chosen" — the tool is one command away
  (`graphify install --platform claude` / `--platform codex`, then `/graphify .` in
  this repo) rather than requiring a fresh install.
- Need: persistent map of cross-file relationships so an agent doesn't re-derive
  architecture every session.
- Evidence for need: repo has 18 features and non-trivial cross-cutting rules (density,
  theming, sidebar chord handling) — but nearly all of this is already written down,
  by a human, with the *why* attached (see `CLAUDE.md`'s density/sidebar/chart
  sections). A generated graph captures *what* connects to *what*, not *why*, which is
  exactly the part that matters most in this repo's existing docs.
- Official source checked: `github.com/Graphify-Labs/graphify` — real, active project
  (releases every 1–5 days, latest v0.9.55, 2026-09-05), Apache-2.0, PyPI package
  `graphifyy` v0.9.55 by `captainturbo`, local tree-sitter parsing (no code leaves the
  machine for code analysis; only doc/media summarization optionally calls an LLM).
  Confirmed independently via the release page and PyPI, not taken from a single
  possibly-hallucinated summary.
- Current solution: `CLAUDE.md` + `AGENTS.md` architecture invariants +
  `docs/design-system/` + `docs/frontend-platform/` + oxlint-enforced feature
  boundaries. This is a hand-maintained, intent-carrying knowledge graph in prose.
- Gap: real but small — mostly "what imports what across features," which `rg` and the
  oxlint override list already answer in a repo this size (346 files).
- Decision: **LATER — but cheap to flip to now, since it's already installed.** The
  need argument above is unchanged: at 346 files with thorough hand-written docs, the
  marginal benefit is still small today. What changed is the cost side — activation is
  local, free, and doesn't touch the repo (skill registration is a global
  `~/.claude/skills/`/`~/.codex/skills/` write; building the graph writes to a
  `graphify-out/` directory you'd choose whether to commit or gitignore). Not run in
  this pass because the follow-up request explicitly said not to install or change
  anything new — this is presented as a one-command option for you to trigger
  yourself, not applied.

### 3.3 Serena (symbol/LSP code intelligence) — **LATER (two independent axes, kept separate per explicit instruction)**

Re-verified 2026-09-07, second pass, against fresh fetches of the advisory pages and
the releases page (not carried forward from the first pass). The two questions below
are evaluated independently and are not allowed to bleed into each other.

**Axis A — is the current patched version safe to use, security-wise?**
- `github.com/oraios/serena/releases`: current latest stable is **v1.7.0** (2026-08-09).
  No release since (as of 2026-09-07, ~4 weeks quiet).
- `GHSA-pp25-4cg4-qcr9` (Critical, CVSS not restated here, SSTI in mode/context prompt
  rendering → RCE on project activation, bypasses `trusted_project_path_patterns`,
  published 2026-08-09): **affected ≤1.6.1, patched in 1.7.0.** 1.7.0 was released the
  same day as this advisory — i.e., the fix shipped concurrently with disclosure.
- `GHSA-37h2-6p4f-mp3q` (High, CVSS 8.3 / CVE-2026-49471, unauthenticated Flask
  dashboard on fixed port 24282 + DNS rebinding + `shell=True` in
  `execute_shell_command` → RCE via poisoned memory file, published 2026-07-01):
  **affected <1.5.2, patched in 1.5.2.** 1.5.2 predates 1.7.0, so this is also closed
  in current stable.
- **Answer: yes** — pinned to `>=1.7.0`, there is no currently-known unpatched
  vulnerability in Serena as of this check. Separately worth naming as a
  maintenance-activity signal (not a current-exploitability claim): two severe RCE
  disclosures five weeks apart, both against an agent-tool design that runs a local
  unauthenticated dashboard and shells out with `shell=True`, is a higher disclosure
  rate than most tools evaluated in this document. That's a "watch the security tab
  going forward" note, not a reason 1.7.0 itself is unsafe today.

**Axis B — does this repo, at its current scale, actually need Serena's benefit?**
- Repo is 346 files / ~35k LOC, single package, not a monorepo.
- `rg` (ripgrep) is already installed and in routine use; the built-in Explore agent
  already does targeted symbol/reference retrieval; `AGENTS.md`/`CLAUDE.md` already
  name every major file's role and cross-cutting invariant by hand, with the *why*
  attached — the exact thing LSP-level tooling can't give you.
- `npm run typecheck` (`tsc -b`) already catches the specific class of error
  (broken references, wrong types) that symbol-navigation is partly meant to prevent
  agents from introducing.
- **Answer: not proven today.** This is a scale/evidence judgment, independent of
  Axis A — it would hold even if Serena had a spotless security record.

- Decision: **LATER**, not a security-driven REJECT. Axis A is currently satisfied
  (1.7.0 is usable); Axis B is what's actually withholding adoption. If this repo
  grows substantially (multi-package, a real backend + shared types, LOC growth that
  makes `rg` noisy), re-run Axis B on the numbers at that time, and re-run Axis A
  fresh against whatever the then-current release and `security/advisories` page say
  — do not carry forward "1.7.0 was fine in September" as a standing security
  clearance.

### 3.4 Context7 (current library docs) — **LATER**
- Need: this repo runs several libraries the model's training data likely
  under-represents at their current majors — React 19.2, Tailwind v4.3, TanStack Query
  v5 / Table v9, Zod 4.5, react-hook-form 7.87, i18next 26 / react-i18next 17,
  recharts 3.8. Guessing at deprecated APIs for these is a real, recurring risk class,
  not a hypothetical.
- Official source checked: `github.com/upstash/context7`. Setup: `npx ctx7 setup`
  (Node 18+), OAuth + generated API key, MCP mode or CLI+Skills mode. Hosted endpoint
  `https://mcp.context7.com/mcp`; a local MCP server variant also exists in the repo.
  Docs state library content is community-contributed and accuracy isn't guaranteed;
  the query goes to Context7's backend (library/API name + question), not your source
  code.
- Current solution: none — the model answers from training data only.
- Gap: real, and it's the one candidate in this document with a clean, low-risk use
  case (doc lookup, not code/shell access).
- Shared-infra caveat (important — checked against official Codex docs): Codex's MCP
  configuration lives in the user's global `~/.codex/config.toml`, not a project file,
  while Claude Code's project MCP config is `.mcp.json` in the repo. There is **no
  single repo file that wires Context7 into both agents** — you'd configure it once per
  agent, on your machine, not once in the repo. That weakens the "shared repo
  infrastructure" framing from PDF §6/§13 for this specific tool; it's a personal
  productivity aid for whichever agent you wire it into, not a repo contract item.
- Decision: **LATER**, not INSTALL. The doc-staleness need is real, but per this
  document's own §6 goal (shared Claude/Codex infrastructure, not per-agent personal
  config), a tool that requires a separate account, a separate API key, and separate
  machine-level config for each agent — with no repo file wiring either of them up —
  doesn't clear the bar this review held `mise` to (rejected for adding a second,
  agent-agnostic vocabulary next to what already works). If you personally want it for
  your own Claude Code sessions regardless of Codex parity, that's a reasonable
  individual call — just not a "repo contract" decision, so not applied here.

### 3.5 Playwright (E2E/browser validation) — **LATER**
- Need: verify UI changes actually work end-to-end.
- Evidence against urgency: no backend, no production users, no critical multi-step
  flow (auth is explicitly mocked per `CLAUDE.md`'s "High-Risk Areas" section, which
  names `src/features/auth` as the *future* risk area, not a current one).
- Current solution: `claude-in-chrome` ad hoc browser tooling, already referenced in
  `AGENTS.md` ("use available browser tooling to inspect the affected page... both
  themes"). This covers today's actual need (visual/theme verification) without a new
  dependency, config, or CI job.
- Decision: **LATER**. Revisit once a real backend and real user-facing critical paths
  (checkout, payments, tenant-scoped data) exist — matches the PDF's own Wave 3 timing
  and this repo's own "High-Risk Areas" note.

### 3.6 mise (tool/version/task standardization) — **REJECT; cheaper fix applied instead**
- Need per PDF's own criterion: *"CI Node 24 kullanıyor, README Node 22 diyor"* — i.e.
  mise earns its place when something in the repo actually **disagrees** about
  versions.
- Evidence: nothing disagreed. No `.nvmrc`, no `.node-version`, no `engines`, no CI to
  contradict anything (until this change added CI).
- Decision: **REJECT** as a tool. Instead added an `engines.node` field to
  `package.json`, revised twice:
  - First (Codex-audit fix pass): narrowed from Vite's own full supported range
    (`"^20.19.0 || >=22.12.0"`) to `">=22.12.0"`, since CI only runs Node 22 and nothing
    in the repo exercised the 20.19–22.11 branch.
  - Second (this pass): `>=22.12.0` was itself still an unverified, open-ended claim —
    it says "22.12 and anything newer, forever" without checking whether the actual
    installed dependency graph agrees. Checked directly against
    `package-lock.json`'s own recorded `engines` for the packages that matter most here
    (`vitest@5.0.0`: `^22.12.0 || ^24.0.0 || >=26.0.0`; `jsdom@29.1.1`: `^20.19.0 ||
    ^22.13.0 || >=24.0.0`; `vite@7.3.6`: `^20.19.0 || >=22.12.0`;
    `@testing-library/jest-dom@7.0.1`: `>=22`). Intersecting all four within the Node 22
    line — the only line CI tests — the binding floor is `jsdom`'s `^22.13.0`, not
    `vitest`'s `^22.12.0`: `22.12.x` satisfies vitest/vite but fails jsdom. The
    open-ended `>=22.12.0` also implicitly claimed Node 24/25/26+ support that no
    dependency here actually promises uniformly (`vitest` explicitly excludes Node 23
    and 25) and that CI has never run. Narrowed to `"^22.13.0"` — bounded to the one
    line of Node that both the dependency graph and CI actually agree on. No version
    manager (mise or otherwise) added; this is a `package.json` field, matching a real,
    checked constraint rather than the widest range that happens not to error today.

### 3.7 ast-grep (AST search / codemod) — **REJECT (need already met)**
- Need: safe structural search/rewrite for repo-wide invariants.
- Evidence: the one invariant that would need this — "features never import each
  other" — is **already enforced today**, via `.oxlintrc.json`'s per-feature
  `no-restricted-imports` overrides (one block per feature, ~19 blocks).
- Gap: not the tool — the *pattern*. Each override block is hand-written and grows
  O(n²) as features are added (adding feature #19 required touching the other 18
  blocks' exclusion lists). That's a real maintenance risk, but it's a lint-config
  authoring problem, not a missing-tool problem; ast-grep wouldn't remove the O(n²)
  shape, it would just let you *find* violations differently.
- Decision: **REJECT** ast-grep as a new dependency. **Flagged as a risk, not fixed**
  in this change (out of this review's scope — it's a lint-config design change, not
  an AI-tooling stack decision); worth a follow-up ticket to generate that override
  block from the `src/features/*` directory listing instead of hand-maintaining it.

### 3.8 gh (GitHub CLI) — **INSTALL (needs your confirmation, machine-level)**
- Need: PR/CI/issue operations. This repo has a real GitHub remote, and Claude Code's
  own built-in PR-creation workflow already assumes `gh` is present.
- Evidence: `gh` is **not installed** on this machine (checked directly, not assumed).
  `git remote -v` shows a real GitHub origin.
- Current solution: none — PR creation would currently fail or fall back to manual
  steps.
- Decision: **INSTALL — recommended, not applied**. This is a `brew install gh`
  on your machine, outside the repo and outside git; per this session's own safety
  rules, that needs your explicit go-ahead before running, even though the case for it
  is clear.

### 3.9 Terminal productivity set (Atuin, ripgrep, fd, fzf, bat, delta, lazygit, zoxide, Ghostty) — **REJECT as repo contract; personal choice otherwise**
- Need per PDF: reduce terminal/search friction.
- Evidence: `ripgrep` is already installed; the rest are absent.
- Decision: **REJECT** as anything this document mandates — none of these change agent
  determinism, none travel to CI or a teammate's machine, none are read by
  `AGENTS.md`/`package.json`. They're a personal terminal setup choice with zero
  repository blast radius either way. Not installed, not recommended for or against.

### 3.10 Renovate — **LATER**
- Need: automated dependency PRs.
- Evidence: dependencies are already fairly current (React 19.2, Tailwind v4.3, Zod
  4.5 — actively hand-maintained), and until this change there was no CI to validate
  an automated PR against.
- Decision: **LATER**. Now that CI exists (§4), a Renovate app install (a GitHub-side,
  org-level grant, not a code change) becomes worth considering — but that's your
  call to make on github.com, not something to script here.

### 3.11 Secret scanning (Gitleaks) — **INSTALL — applied, via the CLI directly, not gitleaks-action**

Revised 2026-09-07 in the Codex-audit fix pass, then **revised again the same day in a
second fix pass** after Codex found the first revision's own range logic still had
unproven gaps and one unverified factual claim. Corrections made in this second pass,
each re-verified against a primary source before being written down:

- **The "20 entries" webhook cap claim was wrong — retracted.** GitHub's own webhook
  payload docs (`docs.github.com/en/webhooks/webhook-events-and-payloads#push`, fetched
  directly) state the `push` event's `commits[]` array "includes a maximum of 2048
  commits," not 20. The number 20 was never sourced from gitleaks-action's code or
  GitHub's docs in the first pass — it was an unverified assumption written with more
  confidence than the evidence supported, exactly the failure mode this second pass was
  asked to check for. It is corrected everywhere it appeared (this file, `ci.yml`'s
  comments).
- **What is still a confirmed, source-verified defect in gitleaks-action**
  (`src/index.js`/`src/gitleaks.js`, fetched directly from
  `github.com/gitleaks/gitleaks-action`):
  - On `pull_request`, `headRef` comes from `commits.data[commits.data.length -
    1].sha`, where `commits.data` is one **unpaginated** call to `GET
    /repos/{owner}/{repo}/pulls/{pull_number}/commits` — no `per_page` set, so it uses
    the REST API's documented default page size of 30
    (`docs.github.com/en/rest/using-the-rest-api/using-pagination-in-the-rest-api`). A
    PR with more than 30 commits gets a `headRef` silently pinned to the 30th commit,
    not the true tip — everything after it is never scanned, with no error raised.
  - On `push`, `baseRef`/`headRef` come from indexing into the webhook's `commits[]`
    array (`commits[0]`, `commits[length-1]`) rather than from GitHub's purpose-built
    `before`/`after` fields for this exact use. Even with the 2048 cap corrected, this
    remains a fragile proxy: the array's ordering and membership rules for
    non-fast-forward pushes are not the same guarantee `before`/`after` give directly.
  - Neither path raises an error when it truncates — the job reports success having
    scanned less than the real range, which understates risk rather than overstating
    it. (The first pass's fix pass called this "fail closed"; that term means the
    opposite of what's happening here — a silent, undetected gap is fail-*open*, not
    fail-closed — and has been removed.)
  - gitleaks-action's own log-opts for both event types were `--no-merges
    --first-parent base^..head` — a second, independent gap from the truncation issue
    above, carried forward uncorrected into this fix's own first revision (see next
    bullet).
  - `detect` (what the first revision called) has been deprecated since gitleaks
    v8.19.0 per the project's own README (`⚠️ v8.19.0 introduced a change that
    deprecated detect and protect`); it still runs but the documented commands are now
    `git` (repo/history scanning) and `dir` (working-tree scanning).
- **This pass's own defects, found and fixed:**
  - The first revision kept gitleaks-action's `--no-merges --first-parent` restriction
    on the replacement's `--log-opts`. Proven wrong with a real git fixture
    (`.github/scripts/test-gitleaks-range.sh`, "branch-merged-into-PR" case): a commit
    that exists only on a branch merged into the PR branch before the PR's own tip
    commit is reachable in a plain `base..head` walk but invisible under
    `--first-parent`. Fixed by dropping both flags.
  - Dropping `--first-parent`/`--no-merges` alone still misses one thing: `git log -p`
    suppresses the diff of merge commits by default, so content introduced only during
    manual conflict resolution (not present in either parent) never appears in patch
    output. Proven with a second fixture ("merge-commit" case: a merge commit whose
    resolved file contains a token absent from both parents is invisible to plain `git
    log -p base..head`, but appears once `-m` is added, which makes git show the merge
    commit's diff against each parent instead of skipping it). Fixed by adding `-m`.
  - The zero-before-SHA (initial push) case previously fell back to `-1` (scan only the
    single most recent commit) — silently missing every other commit in a multi-commit
    initial push. Fixed: an empty/zero base now means "no restrictive range at all,"
    scanning full history from `HEAD` via `gitleaks git`'s own default behavior when no
    range is given.
  - History scanning (`git log -p`, diffs only) cannot see a secret that predates the
    current push/PR and sits unchanged in the tree — a diff-based scan only shows lines
    that changed. Added a second, separate `gitleaks dir` step that scans the checked-
    out working tree directly, independent of history, to cover that case.
- **Fix applied**: `.github/workflows/ci.yml`'s `secrets` job computes the scan range
  from GitHub's trusted, purpose-built fields —
  `github.event.pull_request.base.sha`/`head.sha` for PRs, `github.event.before`/
  `github.sha` for pushes — via `.github/scripts/gitleaks-range.sh`, converts that into
  `--log-opts` via `.github/scripts/gitleaks-log-opts.sh` (`-m base..head`, or `-m`
  alone when there is no base), then runs two scans through the official `gitleaks`
  CLI: `gitleaks git --redact=100 --log-opts="$LOG_OPTS" .` for history and `gitleaks
  dir --redact=100 .` for the final tree — through the official image
  `ghcr.io/gitleaks/gitleaks`, pinned by digest to `v8.30.1`
  (`sha256:c00b6bd0aeb3071cbcb79009cb16a60dd9e0a7c60e2be9ab65d25e6bc8abbb7f`, resolved
  directly from the GHCR registry API). `--redact=100` is new in this pass too: verified
  against the CLI's own `--help` output that `--redact` is opt-in (nothing redacts
  secrets from verbose/log output unless the flag is passed), so `-v` without it would
  have printed any matched secret's raw value into CI logs.
  `.github/scripts/test-gitleaks-range.sh` runs as the job's first step and exercises
  six git fixtures (linear PR, multi-commit PR past the old action's 30-commit page
  size, a branch merged into a PR branch, a merge commit with conflict-resolution-only
  content, a normal push, and a zero-before initial push) before the range it computes
  is trusted for the real scan — this is checked-in, re-runnable evidence, not a
  one-off manual command.
  Locally verified without Docker (no daemon running in this environment): downloaded
  the `v8.30.1` `gitleaks` binary directly from GitHub releases and ran `gitleaks git
  --log-opts="-m HEAD~3..HEAD"` against this repo's real history, confirming it
  reported exactly 3 commits scanned. The containerized `docker run` invocation itself
  is still CLI-verified, not container-verified end-to-end — unchanged from the first
  pass, still flagged as handoff item 5.
- **What this actually guarantees, stated narrowly on purpose**: the history scan
  covers exactly the commits newly reachable in this push/PR (including side-branch and
  merge-commit content, per the fixtures above); the tree scan covers the full checked-
  out working tree on every run regardless of history. Together they catch (a) a secret
  added and later removed within the same push/PR, and (b) a secret present in the
  current tree however it got there. They do **not** retroactively scan the repository's
  full history before this job existed — a secret already committed and still present
  in the tree today would be caught by the tree scan on the next run; one already
  removed from the tree before this job existed would not be caught at all. That's a
  known, accepted gap for a repo with `--redact` disabled everywhere historically (no
  evidence anything was ever committed to this repo's history to date), not a claim
  that this pipeline provides full-history retroactive coverage.
- **Traded away**: gitleaks-action's PR inline comments, SARIF upload, and job-summary
  write-up are gone — this replacement fails the job on a nonzero exit and nothing
  more. Kept simple deliberately rather than re-implementing that UX on the CLI;
  re-adding it later is a legitimate LATER, not required for correctness.
- Need (unchanged from the original pass): deterministic enforcement of the existing
  "never commit secrets" rule from your global `CLAUDE.md`; `.gitignore` didn't cover
  `.env` until the first review pass (§4) — that gap is closed, not open.
- Decision: **INSTALL — applied**, as a CI job, not a local pre-commit hook — this
  sandbox cannot write to `.claude/hooks/`, and a server-side CI check is agent-agnostic
  (works the same whether Claude, Codex, or a human pushes) rather than relying on
  every contributor's local hook being installed.

### 3.12 Storybook — **LATER**
- Need: cataloged component states/interactions.
- Evidence: `docs/design-system/` already documents states extensively in prose; no
  design-system consumer beyond this one repo yet.
- Decision: **LATER** — real value once the design system has more than one consumer
  or the team grows past one developer; not urgent today.

### 3.13 Formatter (Oxc `oxfmt` vs. Prettier) — **LATER, evaluated not applied**
- Need: no formatter exists today (`oxlint` is a linter, not a formatter); style
  consistency currently depends on editor settings alone.
- Official source checked: `oxc.rs` formatter docs — `oxfmt` claims Prettier-compatible
  output and passes the Prettier JS/TS conformance suite, and is from the same Oxc
  project already providing `oxlint`, avoiding a second toolchain vendor. Checked npm
  directly: current published version is **0.66.0** — pre-1.0, so treat it as still
  stabilizing, not "done."
- Decision: **LATER, not applied**. Adding the dependency and script is cheap, but a
  first repo-wide format run on 346 files is a large, review-hostile diff and a real
  decision about *when* to take that hit — not something to do silently inside an
  infrastructure review. Recommended default if/when you want it: `oxfmt` over
  Prettier (same vendor as the linter, one less toolchain), applied as its own,
  isolated commit.

### 3.14 Testcontainers (.NET), database tooling, Redis, backend observability (OpenTelemetry .NET, etc.) — **NOT APPLICABLE / LATER**
- Evidence: this repository has no backend, no database, no queue, no Redis — verified
  directly (no server code, no ORM, no connection config, no `docker-compose.yml`, data
  is mocked behind MSW/React Query). The PDF's own target environment assumes a ".NET 8
  backend"; this repo proves none of that.
- Corrected framing (this pass removes an overreach from the first pass): the first
  version of this document additionally asserted Fynovio's *eventual* backend would be
  Laravel/PHP, based on a **sibling repository's** CLAUDE.md (`~/Projects/fynovio/CLAUDE.md`,
  which documents the `erp-center`/`crm-app`/other Laravel projects in the `fynovio/`
  folder). That may be true of those other projects, but nothing in *this*
  repository — its `AGENTS.md`, its `CLAUDE.md`, its code, or its `.env.example`
  (`VITE_API_URL` is generic, not framework-specific) — commits this repo to any
  particular future backend language. Asserting Laravel here was an inference from a
  neighboring project, not evidence from this one, and is retracted.
- Decision: **NOT APPLICABLE / LATER** for every item in this category (Testcontainers,
  a specific database, Redis, backend-specific observability). None of it can be
  correctly scoped without knowing what backend (if any) this repository eventually
  gets, and that isn't decidable from this repo today. When a backend is chosen here,
  re-run this same need-gate against whatever that stack's actual tools are — do not
  carry forward either ".NET" (the PDF's assumption) or "Laravel" (this document's
  earlier, retracted inference) as a default.

### 3.15 Linear MCP, GitHub MCP, Worktrunk, Graphite CLI, Dev Containers — **REJECT/LATER, all premature**
- Linear MCP: no evidence Linear is used anywhere in this repo or its docs. **REJECT.**
- GitHub MCP: `gh` (once installed) plus existing tools already cover today's PR/issue
  needs per the PDF's own "gh first, MCP only when needed" guidance. **LATER.**
- Worktrunk: PDF's own threshold is "5–10+ concurrent worktrees"; this repo has one
  worktree, one active branch. **REJECT for now, cheap to add later.**
- Graphite CLI (stacked PRs): no CI existed to make PR size/review friction visible
  yet; revisit once CI (added here) and real PR volume exist. **LATER.**
- Dev Containers: solo developer, one machine, no "works on my machine" incidents
  reported. **LATER** — real value once a second developer joins.

## 4. Applied changes (this review, both passes)

All changes are plain file edits, reviewable and revertible via git; nothing was
installed on the machine and no destructive command was run.

**First pass (initial stack review):**
- `package.json`: added `engines.node` and `setup`/`typecheck`/`check` scripts.
- `.gitignore`: added explicit `.env` / `.env.*` (with `!.env.example` preserved) —
  the prior bootstrap pass had flagged this gap and left it unfixed.
- `AGENTS.md`: corrected the stale "no test suite, do not invent npm test" claim
  (vitest has existed with 2 test files/26 tests since before this review). This was a
  live Claude/Codex-contract defect — Codex was being told not to run a suite that exists.
- `docs/claude/BOOTSTRAP-REPORT.md`: first corrections to its stale verification-block claims.
- `.github/workflows/ci.yml`: created — first CI this repository has had.
- Moved `Claude_Code_Modern_Development_Stack_2026.pdf` into `docs/claude/`.

**Second pass (independent Codex audit → verified and fixed here):**
- `package.json`: `engines.node` narrowed from `"^20.19.0 || >=22.12.0"` to
  `">=22.12.0"` (later narrowed again to `"^22.13.0"` in the third pass — see below) —
  the wider range claimed Node 20 support that nothing in this repo tests (§3.6). `setup` script changed from `"npm install"` to `"npm ci"` — a fresh
  clone/worktree bootstrap should be exact and lockfile-driven; `npm install <pkg>` is
  now the documented path for adding/upgrading a dependency (§ Setup contract, AGENTS.md).
- `package-lock.json`: regenerated (`npm install --package-lock-only`) after the
  `engines`/script edits — `npm ci` (which CI's first step runs) fails on a lockfile
  that doesn't match `package.json`; verified by running `npm ci` clean afterward.
- `.github/workflows/ci.yml`: rewritten —
  - Added top-level `permissions: contents: read` (minimum for both jobs; neither
    reads secrets, writes to the repo, or comments on PRs).
  - Both `actions/checkout` steps pinned to the immutable commit SHA for v7.0.1
    (resolved from `api.github.com/repos/actions/checkout/tags`, not guessed) with
    `persist-credentials: false`, so the ephemeral repo token isn't left on disk for
    later steps that don't need it. `actions/setup-node` likewise pinned to v7.0.0's SHA.
  - `on:` quoted as `"on":` — confirmed via a real YAML parse that the bare `on:` key
    is read back as the boolean `true` by standard YAML 1.1 parsers (GitHub's own
    workflow parser special-cases this correctly, but quoting removes the ambiguity
    for any other tool that touches this file).
  - The `secrets` job's use of `gitleaks/gitleaks-action@v3` was replaced entirely
    after finding a real, sourced defect in its commit-range logic — see §3.11 for the
    full evidence and the replacement (direct `gitleaks` CLI via a digest-pinned
    `ghcr.io/gitleaks/gitleaks` image, fed an exact range from uncapped webhook fields).
- `AGENTS.md`: consolidated — merged in every architecture/design invariant that
  previously existed *only* in `CLAUDE.md` (density mechanics, sidebar chord/BracketLeft
  detail, radii ladder, elevation/shadow split, accent/tint aliasing, chart
  palette/`STATIC_MARK` rule, pipeline `STAGES`/`closeDate` nullability, theme
  resolution detail) so there is one architecture source of truth instead of two that
  could drift. Added a `## Worktrees` section (one task = one branch = one worktree =
  one agent session; `.worktrees/` convention; fresh-worktree `npm run setup` +
  `.env*` absence is safe-by-default). Updated the commands table for `npm ci` vs
  `npm install <pkg>` and removed the hardcoded `localhost:5173` assumption (Vite's
  actual reported URL was already the documented answer; the hardcoded port lived only
  in `CLAUDE.md`, removed in the next bullet).
- `CLAUDE.md`: replaced its entire body with `@AGENTS.md` (Claude Code's own
  documented import syntax, confirmed against `code.claude.com/docs/en/memory`) plus a
  short Claude-specific section (the `test-verifier` subagent, `claude-in-chrome`
  browser verification). All the architecture/design content this file used to carry
  independently now lives only in `AGENTS.md`, closing the duplicate-source-of-truth
  gap Codex flagged. The bootstrap markers are preserved.
- `.claude/agents/test-verifier.md`: replaced its hardcoded, independently-drifting
  command list with an explicit pointer to `AGENTS.md`'s command table as the single
  source of truth, plus a rule that `AGENTS.md` wins if the two ever disagree.
- `.gitignore`: added `.worktrees/` and `worktrees/` — matching this machine's actual
  `using-git-worktrees` skill convention (checked the skill file directly rather than
  assuming the `.claude/worktrees/` path named in the audit request, which isn't this
  project's established convention).
- `docs/claude/BOOTSTRAP-REPORT.md`: further corrections — the "AGENTS: built-ins
  only, no custom agent" claim (three separate places in the file) directly
  contradicted the same document's own manifest, which lists `test-verifier.md` as a
  created artifact; the "four artifacts" / "test-less, CI-less" FINAL STATE claim was
  off by one artifact and stale even at the time it was written; the
  `.claude/settings.local.json` description ("machine-local git remote/push allow
  rules") didn't match the file's actual content (design-canvas-skill commands plus
  broad install wildcards). All corrected inline with a note explaining what changed
  and why, rather than silently rewritten.
- `.claude/settings.local.json` (machine-local, gitignored, **not** a repo change):
  removed the broad `Bash(npx *)`, `Bash(brew install *)`, and `Bash(uv tool *)`
  allow rules via the `update-config` skill (direct writes to this file are
  sandbox-denied). These three wildcards granted unattended install/execute
  capability from npm, Homebrew, and PyPI-via-uv with no per-command confirmation —
  this session's own finding that `graphifyy` was installed via exactly
  `uv tool install` today is direct evidence of what that grant permits. The narrow,
  exact commands and read-only `command -v`/`--version` checks already in the file
  were left untouched.
- Moved `Claude_Code_Modern_Development_Stack_2026.pdf` (staged at repo root) into
  `docs/claude/`, next to the existing sibling reference PDF, via `git mv`.
- This file.

Nothing else was touched. No `.claude/settings.json`, `.claude/skills/`, or
`.claude/hooks/` changes were attempted — this session's sandbox denies writes there
by design; any of those would need `/update-config` or your own hand.

**Third pass (second independent Codex audit, same day → verified and fixed here):**
This pass specifically checked the *previous* pass's own work for unproven claims and
unverified numbers, and found several — corrected below, each against a primary
source, not re-asserted from memory.
- `package.json`: `engines.node` narrowed again, from `">=22.12.0"` to `"^22.13.0"`,
  after intersecting the actual `engines` fields recorded for `vitest@5.0.0`,
  `jsdom@29.1.1`, `vite@7.3.6`, and `@testing-library/jest-dom@7.0.1` in
  `package-lock.json` — see §3.6 for the full intersection. `>=22.12.0` was itself
  still an unverified, open-ended claim.
- `package-lock.json`: not regenerated this pass — `engines` is metadata only and
  doesn't affect dependency resolution, so the existing lockfile stays valid; confirmed
  by a clean `npm ci` afterward (§ Final validation).
- `.github/workflows/ci.yml` / `.github/scripts/`: the `secrets` job's range and
  scan logic was rebuilt — see §3.11 for the full defect list (the retracted "20
  commits" claim, the still-real 30-commit PR pagination default, the
  `--first-parent`/`--no-merges` coverage gap this pass's own prior revision had
  carried forward uncorrected, the zero-before `-1` fallback, and the missing
  final-tree scan) and the fix (trusted-field range resolution, `-m` instead of
  `--first-parent`/`--no-merges`, `--redact=100`, `gitleaks git`/`gitleaks dir`
  instead of deprecated `detect`, a second tree-scan step). The range/log-opts logic
  was extracted into two small, argument-driven scripts
  (`.github/scripts/gitleaks-range.sh`, `.github/scripts/gitleaks-log-opts.sh`) purely
  so it could be unit-tested against real git fixtures rather than only read by eye —
  `.github/scripts/test-gitleaks-range.sh` covers linear PR, multi-commit PR, a branch
  merged into a PR branch, a merge commit with conflict-resolution-only content, a
  normal push, and a zero-before initial push (14 assertions, all passing — §
  Final validation), and now runs as the `secrets` job's first CI step so a future
  change to this logic can't silently regress without the job failing first.
- `AGENTS.md`: the "Design system" section's radii ladder, compact-row-height figures,
  and the accent-gradient contrast ratio were literal numbers copied from the old
  `CLAUDE.md` in the second pass, presented as facts without checking them against the
  files this document itself calls canonical. Checked `src/styles/tokens.css` directly:
  the radii ladder text ("16px tenant row · 18px user row") is itself a stale artboard
  comment in `tokens.css` that doesn't match tokens.css's own `--nx-r-*` variable list
  (there is one `--nx-r-ctl-lg: 18px`, no separate 16px step) — AGENTS.md had copied a
  comment that was already wrong at its source. The contrast ratio duplicates a value
  already documented in `docs/design-system/11-accessibility.md`. Both were replaced
  with references to the canonical file/token instead of restated numbers, per this
  pass's instruction not to treat values inherited from the old `CLAUDE.md` as
  canonical truth without checking them against the files that actually are.
- `docs/claude/BOOTSTRAP-REPORT.md` / this file: corrected "every push/PR" to match
  the workflow's actual triggers (push to `main`, and pull requests against any
  branch); fixed `CLAUDE.md`'s bootstrap-manifest path reference (it named
  `docs/claude/.bootstrap-manifest.json`; the file actually lives at
  `.claude/.bootstrap-manifest.json`); removed a misused "fail closed" in §3.11 (the
  behavior described — a truncated scan reporting success — is fail-*open*, the
  opposite of what that term means); rewrote §6's `settings.local.json` bullet, which
  still described the wildcard permissions as "flagged, not changed" after the second
  pass had already removed them.

## 5. Worktree readiness (the genuinely new analysis this task asked for)

Today: one worktree, one branch (`ios-27`). For "1 task = 1 branch = 1 worktree = 1
agent session" to work later without collisions:

- **Ports**: not a real risk. Vite has no `strictPort` set, so it auto-increments on
  collision, and `AGENTS.md` already tells an agent to "use the URL Vite actually
  reports" rather than assume `:5173`. No change needed.
- **`node_modules`**: a fresh `git worktree add` does **not** copy `node_modules` — each
  new worktree needs its own `npm install` before `dev`/`build`/`test` work. This is
  the actual first-run failure mode a second agent would hit, not port collision.
- **`.env*` files**: untracked (per `.gitignore`, now including `.env`/`.env.*`), so a
  fresh worktree starts without them. Since `.env.example` documents `VITE_API_URL`
  and `VITE_API_MOCKING`, and the app degrades safely to MSW-mocked mode with no
  `.env` present, an agent in a fresh worktree gets *correct-by-default* mocked
  behavior — not a silent misconfiguration. Worth stating explicitly so an agent
  doesn't "fix" a missing `.env` by inventing one.
- **Caches/generated files**: `dist/`, `node_modules/.tmp/*.tsbuildinfo` are per-worktree
  by construction (they live under the worktree's own directory tree) — no shared-state
  risk found.
- No change applied here beyond documenting it (this section) — there was no code
  gap to fix, only an undocumented assumption to make explicit for whichever agent
  hits it first.

## 6. Security notes surfaced, not all in scope to fix

- `.claude/settings.local.json` (git-ignored, machine-local, not part of this repo's
  shared contract) previously allow-listed `Bash(npx *)`, `Bash(brew install *)`, and
  `Bash(uv tool *)` unconditionally — broader standing grants than anything discussed
  above. This was originally flagged here as informational only ("not changed, outside
  this review's authority"); the Codex-audit fix pass revisited that framing and
  removed the three wildcards via the `update-config` skill (§4, "machine-local"
  bullet) — narrowing a developer's own local permissions is machine-local hardening,
  not a repo-contract change, so it didn't need to stay out of scope. This bullet is
  now a record of what was found and fixed, not an open item.
- GitHub-hosted secret scanning / push protection coverage for this specific repo
  (public vs. private, org vs. personal) wasn't independently confirmed (an
  unauthenticated API check returned 404, which is inconclusive) — the CI-level
  `gitleaks` job added here doesn't depend on that and covers the gap either way.
- Serena's two 2026 RCE advisories (§3.3) are both patched in the current stable
  release (1.7.0) — verified independently against `github.com/oraios/serena/security`
  and the individual GHSA pages, not taken on the PDF's word. They inform the "watch
  this project's security tab" note in §3.3, but are explicitly *not* the reason
  Serena is deferred here — the deferral is a separate, scale-based need judgment
  (Axis B in §3.3), kept apart from the security question (Axis A) per explicit
  instruction not to conflate the two.

## 7. Relationship to the prior same-day bootstrap pass

`docs/claude/.bootstrap-manifest.json` / `BOOTSTRAP-REPORT.md` (generated the same day,
`claudeCodeVersion 2.1.263`) independently reached the same core conclusion this review
does: no rules/, skills/, hooks/, or MCP were justified by repo evidence, and the
".env not gitignored" + "no CI" gaps were already correctly identified. This review's
contribution on top of that pass is: (a) actually closing those two flagged gaps, (b)
running the PDF's specific candidate list through the same need-gate discipline, and
(c) the worktree/Codex-contract analysis, which the bootstrap pass's scope didn't cover.

## 8. Handoff for independent Codex audit

Ask Codex to verify, independently, without reading this document's conclusions first:

1. Re-run the validation loop cold: `npm run check` and `npm run build`. Confirm the
   same pass/fail result reported in §2.
2. Confirm Codex's own AGENTS.md discovery actually picks up root `AGENTS.md` in this
   repo (not assumed from docs — verify empirically) and that nothing in it now
   contradicts `package.json` scripts (the defect this review fixed in §4).
3. Independently re-verify Serena's two advisories (GHSA-pp25-4cg4-qcr9,
   GHSA-37h2-6p4f-mp3q) are closed in 1.7.0 (Axis A, §3.3), and separately give a
   second opinion on whether the "repo too small to need it" scale judgment (Axis B)
   holds — keep the two axes apart, as this document does; don't let a fresh security
   finding retroactively justify the scale conclusion or vice versa.
3a. Independently verify the Graphify installation-status table in §0 — confirm
    whether `graphify install --platform codex` would actually register a skill
    Codex picks up (this document infers it from `graphify --help`'s subcommand list,
    not from having run it).
4. Check the `.oxlintrc.json` O(n²) override-block risk flagged in §3.7 — confirm
   whether it's actually a live pain point (e.g., check recent commits for repeated
   edits to that file when adding a feature) or a hypothetical.
5. Sanity-check the CI workflow (`.github/workflows/ci.yml`) actually runs green on a
   real push — this review validated the commands locally but the workflow file
   itself hasn't executed on GitHub's runners yet.
6. Flag anything in this document that reads as tool-name bias rather than
   evidence-based reasoning — that failure mode is exactly what §12 of the source PDF
   warns against, and a second independent model is the intended check on it.

## 9. Summary

| | |
| --- | --- |
| **KEEP** | Claude Code native mechanisms (CLAUDE.md, settings.json, test-verifier subagent); npm/Vite/oxlint/vitest toolchain; existing AGENTS.md↔CLAUDE.md split; ripgrep; single-worktree model at current scale |
| **INSTALL (applied)** | `engines` pin, `typecheck`/`check`/`setup` scripts, `.env` in `.gitignore`, GitHub Actions CI (lint+typecheck+test+build), Gitleaks CI job, AGENTS.md/BOOTSTRAP-REPORT.md corrections, PDF relocated to `docs/claude/` |
| **INSTALL (your action needed)** | `gh` CLI (`brew install gh`) |
| **LATER** | Graphify (installed on this machine since 2026-09-07 10:06 but not activated for either agent or this repo — one command away, not applied here), Serena (current 1.7.0 is security-clean; deferred on scale/need alone, not security — see §3.3's two axes), Context7 (real need, but per-agent account/config — not shared repo infra), Playwright E2E, Storybook, Renovate, `oxfmt` formatter, Graphite CLI, Dev Containers, GitHub MCP |
| **NOT APPLICABLE / LATER** | Testcontainers, database tooling, Redis, backend observability — no backend exists in this repo and this repo alone doesn't determine what backend it will eventually get (the earlier "it'll be Laravel" inference from a sibling repo's docs is retracted — see §3.14) |
| **REJECT** | mise (no version conflict exists — cheaper `engines` fix applied instead), ast-grep (need already met by oxlint), Linear MCP (unused), Worktrunk (one worktree today), terminal-productivity set as a *repo* mandate (personal choice, zero repo blast radius) |

"Do nothing" was the right call for most of the PDF's S-tier list at this repo's
current size and risk profile. The two real, cheap wins were CI and the stale-docs fix
— both applied.
