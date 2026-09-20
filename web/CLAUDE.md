<!-- claude-bootstrap:begin -->
@AGENTS.md

## Claude Code

This project's shared development contract (repo map, canonical commands, architecture
invariants, worktree rules, risk boundaries) lives entirely in `AGENTS.md`, imported
above — Codex reads the same file directly. Don't re-add architecture/design content
here; it belongs in `AGENTS.md` so both agents see one copy, not two that can drift.

- Verification subagent: `.claude/agents/test-verifier.md` runs the canonical
  lint/typecheck/test/build commands from `AGENTS.md` with fresh context and reports
  pass/fail evidence — delegate post-implementation verification to it rather than
  re-deriving commands yourself.
- Browser verification: use `claude-in-chrome` tooling for the visual/theme checks
  `AGENTS.md`'s "UI changes" note calls for. The Playwright E2E suite (`e2e/`, `npm run e2e`) covers the
  auth flows against the real API; it is not a substitute for a visual/theme check.
- This block is owned by `docs/claude/CLAUDE-BOOTSTRAP.md`'s bootstrap tooling (see
  `.claude/.bootstrap-manifest.json`); edits inside the markers are expected and
  preserved across re-runs, not overwritten wholesale.
<!-- claude-bootstrap:end -->

## graphify

`graphify-out/` (gitignored, machine-local — see `AGENTS.md`'s repo-map entry for the
shared, agent-agnostic version of this rule) holds a generated code-knowledge graph:
tree-sitter extraction only, no LLM, no API cost, no `GEMINI_API_KEY`/`GOOGLE_API_KEY`
configured or needed. It is a navigation/impact-analysis layer, not a source of truth —
use it to scope down before reading code, then verify anything that matters against the
actual source, tests, and runtime contracts.

- For a codebase question, run `graphify query "<question>"` before grepping — it
  returns a scoped subgraph (nodes/edges/file:line), usually far smaller than a raw
  grep sweep. `graphify path "<A>" "<B>"` for how two things connect,
  `graphify explain "<concept>"` for one node's neighborhood. `GRAPH_REPORT.md` is for
  broad architecture review only, when query/path/explain don't surface enough.
  Skip all of this for a change already scoped to one or two known files — grep/Read
  directly.
- **Freshness is automatic, not manual.** `graphify hook install` registered a
  post-commit hook (rebuilds in the background after every commit) and a
  post-checkout hook (rebuilds on branch switch, once `graphify-out/` already exists).
  Don't run `graphify update` reflexively after every edit — that instruction was
  graphify's own installer default and is overridden here: refresh it yourself only
  right before or after a large refactor or an impact-analysis task, where a stale
  graph could actually mislead. Don't leave `graphify watch` running for ordinary
  development; it's for the rare case of an active multi-file refactor that needs
  live rebuilds.
- The strict-mode `PreToolUse` hooks in `.claude/settings.json` (Bash/Grep/Read/Glob)
  inject a reminder to query the graph before raw exploration. Follow it for
  structural/"how does X work"/cross-file-impact questions; a change already scoped
  to specific files doesn't need it.
- A fresh worktree has no `graphify-out/` yet (gitignored, not copied by
  `git worktree add`), and the post-checkout hook only fires once it already exists —
  run `graphify update .` once there, the same one-time step as `npm run setup`.
- `.gitattributes` pre-registers graphify's merge driver for `graphify-out/graph.json`
  in case it's ever tracked instead of gitignored; not acted on today.
