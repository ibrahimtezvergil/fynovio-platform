# Codex bootstrap run prompt

Open the target repository in Codex and paste the block below. Keep
`docs/codex/CODEX-BOOTSTRAP.md` at that path in the repository, or adjust its path in the prompt.
This provisions the repository; it does not merely review the bootstrap document.

## Default: implement justified improvements

```text
Read docs/codex/CODEX-BOOTSTRAP.md and execute it for this repository.

Detect the actual stack, existing toolchain, applicable instructions, and capabilities
of this Codex session first. Verify any Codex-specific syntax against the installed
runtime and current official documentation before relying on it.

Implement the smallest justified, reversible, repository-local, development-only
improvements, including dev-tooling installs only when all conditions in section 8
are satisfied. Give a short change-set update, then continue without an extra approval
gate for work already authorized. Respect actual sandbox/tool approval requirements.

Preserve existing and uncommitted work. Leave Claude files and personal/global Codex
settings unchanged. Do not stage, commit, push, create branches/worktrees, or run
destructive Git commands. Do not change application behavior, runtime dependencies,
deployment, or shared CI infrastructure; do not provision accounts or paid services.

Use the discovery checkpoints in the specification. Prefer existing mechanisms and
omit anything without demonstrated recurring value. Validate each changed artifact,
inspect new files as well as the diff, and report changes, intentional omissions,
actual verification results, and remaining limitations in chat. Do not create a
separate report or manifest unless the specification's conditions justify one.
```

## Optional run constraints

Append only what you need; these override the corresponding default above.

**Configuration only; no dependency changes:**

```text
Do not install or upgrade dependencies, invoke installers, or modify manifests or
lockfiles. Implement justified Codex instruction/configuration changes and report
development-tooling gaps as recommendations.
```

**Re-run an existing setup:**

```text
Preserve the working setup. If .codex/bootstrap-manifest.json exists, validate it and
compare recorded hashes before changing managed artifacts. Preserve drifted or
unowned content; propose exact repairs where needed. Do not duplicate mechanisms or
rewrite unchanged files merely to refresh metadata.
```

**Durable report:**

```text
Write the concise final report to docs/codex/BOOTSTRAP-REPORT.md as well as summarizing
it in chat. Do not make ordinary Codex sessions load that report.
```

## Audit only

Use this block **instead of** the execution prompt when you want no changes:

```text
Read docs/codex/CODEX-BOOTSTRAP.md as review criteria for this repository. Do not execute its
write/install instructions. Inspect relevant repository and Codex capabilities,
research official documentation as needed, and return a concrete proposed change set
with reasons and validation steps. Do not write files, install dependencies, or
launch autonomous Codex sessions.
```

## Normal work after bootstrap

Do not paste the bootstrap again for each task. Describe the outcome, reproduction
or acceptance criteria, and relevant constraints. Example:

```text
Fix <observed behavior>. Expected: <acceptance criteria>.
Relevant area or reproduction: <paths / steps>. Preserve <specific contract>.
Implement and run the relevant checks; report the result and any remaining gap.
```
