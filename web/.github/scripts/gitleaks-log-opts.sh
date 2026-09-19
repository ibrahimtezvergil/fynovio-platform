#!/usr/bin/env bash
# Turns a base/head pair (from gitleaks-range.sh) into the --log-opts value
# for `gitleaks git`. No --first-parent, no --no-merges: a range that drops
# either can silently exclude a commit that only exists on a merged-in branch
# (--first-parent) or the content a merge commit itself introduces during
# conflict resolution (default `git log -p`, without -m, shows no diff at all
# for merge commits). `-m` makes merge commits show their diff against each
# parent instead of being suppressed. See test-gitleaks-range.sh for the
# fixture that demonstrates both failure modes concretely.
#
# Usage: gitleaks-log-opts.sh <base-or-empty> <head>
# An empty base means "no known prior point" (initial push / zero before SHA)
# — scan full history from head instead of guessing a bounded range.
set -euo pipefail

base="${1:-}"
head="${2:?head commit SHA required}"

if [ -n "$base" ]; then
  echo "-m ${base}..${head}"
else
  echo "-m"
fi
