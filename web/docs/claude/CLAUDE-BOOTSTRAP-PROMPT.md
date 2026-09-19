# Bootstrap run prompt

Paste the block below into Claude Code from the repository root. `docs/claude/CLAUDE-BOOTSTRAP.md` must be present at that path (or adjust the path).

---

```
Read docs/claude/CLAUDE-BOOTSTRAP.md and execute it for this repository.

Constraints for this run:
- Start in plan mode. Present the complete proposed change set before writing any file.
- Do not commit, branch, stage, or run any destructive git command.
- Do not install dependencies and do not modify lockfiles. Report tooling gaps as
  RECOMMENDATIONS.
- Budget: read at most 40 files during reconnaissance; create or modify at most 6
  artifacts. Stop at the cap and report rather than continuing silently.
- Write the final report to docs/claude/BOOTSTRAP-REPORT.md.
- Record every artifact you create in .claude/.bootstrap-manifest.json and stamp
  generated files per section 9.

Before creating anything: detect this repository's actual stack, topology, canonical
commands, and existing tooling; inventory the Claude configuration already present;
and confirm which mechanisms the installed Claude Code version supports. Prefer a
native mechanism (/init, /doctor, .claude/rules/ with paths:, skills, hooks) over
anything custom. A minimal result is a valid result.
```

---

## Variants

**Authorize dev-tooling installs** — replace the dependency line with:

```
- You may install development-only dependencies using the repository's existing
  dependency manager, one at a time, respecting the lockfile. Show me the lockfile
  diff for each. If unrelated churn appears, revert and downgrade it to a
  recommendation.
```

**Re-run on an already-bootstrapped repo** — append:

```
This repository has been bootstrapped before. Read .claude/.bootstrap-manifest.json
first. Report any bootstrap-owned artifact a human has since modified and leave it
untouched. Only repair stale artifacts and add newly justified ones.
```

**Audit only, write nothing** — replace the whole constraints block with:

```
- Do not write any file. Produce only the proposed change set and the report content
  as chat output, so I can review before any run that writes.
```
