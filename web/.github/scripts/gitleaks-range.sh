#!/usr/bin/env bash
# Resolves the exact commit range for the gitleaks history scan from trusted,
# uncapped GitHub Actions context fields — never from a paginated REST call or
# a webhook array. See .github/workflows/ci.yml for why: gitleaks-action's own
# PR-commits lookup (unpaginated, default per_page=30) and its base/head-via-
# commits-array indexing for push events are both fragile in ways this avoids.
#
# Inputs (env):
#   GITHUB_EVENT_NAME  "pull_request" or a push-like event; anything else is
#                      treated as push (workflow_dispatch/schedule have no
#                      meaningful base, so they fall through to full-history).
#   PR_BASE_SHA        github.event.pull_request.base.sha (pull_request only)
#   PR_HEAD_SHA        github.event.pull_request.head.sha (pull_request only)
#   PUSH_BEFORE_SHA    github.event.before (push only)
#   PUSH_AFTER_SHA     github.sha (push only)
#
# Output: prints "base=<sha-or-empty>" and "head=<sha>" on stdout, one per
# line. An empty base means "no known prior point — scan full history from
# head" (initial push, or a push whose before is the all-zero SHA). Also
# appends the same to $GITHUB_OUTPUT when that variable is set, so the script
# doubles as a CI step and a standalone unit-testable command.
set -euo pipefail

ZERO_SHA="0000000000000000000000000000000000000000"

if [ "${GITHUB_EVENT_NAME:-}" = "pull_request" ]; then
  base="${PR_BASE_SHA:?PR_BASE_SHA required for pull_request event}"
  head="${PR_HEAD_SHA:?PR_HEAD_SHA required for pull_request event}"
else
  before="${PUSH_BEFORE_SHA:-}"
  head="${PUSH_AFTER_SHA:?PUSH_AFTER_SHA required for push event}"
  if [ -z "$before" ] || [ "$before" = "$ZERO_SHA" ]; then
    base=""
  else
    base="$before"
  fi
fi

echo "base=$base"
echo "head=$head"

if [ -n "${GITHUB_OUTPUT:-}" ]; then
  {
    echo "base=$base"
    echo "head=$head"
  } >> "$GITHUB_OUTPUT"
fi
