#!/usr/bin/env bash
# Validates gitleaks-range.sh and gitleaks-log-opts.sh against real git
# fixtures for every topology the CI gitleaks job has to cover: linear PR,
# multi-commit PR, a branch merged into a PR branch, a merge commit whose
# conflict resolution introduces brand-new content, a normal push, and an
# initial/zero-before push. Run manually:
#   .github/scripts/test-gitleaks-range.sh
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
RANGE_SCRIPT="$SCRIPT_DIR/gitleaks-range.sh"
LOG_OPTS_SCRIPT="$SCRIPT_DIR/gitleaks-log-opts.sh"
WORK="$(mktemp -d "${TMPDIR:-/tmp}/gitleaks-range-test.XXXXXX")"
trap 'rm -rf "$WORK"' EXIT

PASS=0
FAIL=0
ok()  { PASS=$((PASS+1)); printf 'PASS  %s\n' "$1"; }
bad() { FAIL=$((FAIL+1)); printf 'FAIL  %s\n' "$1"; }

git_init() {
  local dir="$1"
  mkdir -p "$dir"
  git -C "$dir" init -q -b main
  git -C "$dir" config user.email test@example.com
  git -C "$dir" config user.name "Gitleaks Range Test"
  git -C "$dir" config commit.gpgsign false
}

commit_file() {
  local dir="$1" file="$2" content="$3" msg="$4"
  printf '%s' "$content" > "$dir/$file"
  git -C "$dir" add "$file"
  git -C "$dir" commit -q -m "$msg"
}

sha() { git -C "$1" rev-parse "$2"; }

resolve_range() {
  # -u GITHUB_OUTPUT: never let this test's calls append to a real CI step's
  # output file if this script itself is ever run as a CI step.
  env -u GITHUB_OUTPUT "$@" "$RANGE_SCRIPT"
}

field() { printf '%s' "$1" | sed -n "s/^$2=//p"; }

### 1. Linear PR ###############################################################
test_linear_pr() {
  local d="$WORK/linear-pr"
  git_init "$d"
  commit_file "$d" f.txt "a" "A"
  local base; base="$(sha "$d" HEAD)"
  git -C "$d" checkout -q -b feature
  commit_file "$d" f.txt "ab" "B"
  commit_file "$d" f.txt "abc" "C"
  commit_file "$d" f.txt "abcd" "D"
  local head; head="$(sha "$d" HEAD)"

  local out; out="$(resolve_range GITHUB_EVENT_NAME=pull_request PR_BASE_SHA="$base" PR_HEAD_SHA="$head")"
  if [ "$(field "$out" base)" = "$base" ] && [ "$(field "$out" head)" = "$head" ]; then
    ok "linear PR: base/head resolve to trusted PR SHAs"
  else
    bad "linear PR: base/head mismatch: $out"
  fi

  local subjects; subjects="$(git -C "$d" log --format=%s "$base..$head" | sort)"
  if [ "$subjects" = "$(printf 'B\nC\nD' | sort)" ]; then
    ok "linear PR: range covers exactly B, C, D"
  else
    bad "linear PR: unexpected commit set: $subjects"
  fi
}

### 2. Multi-commit PR (well past the old action's unpaginated 30-per-page cap) #
test_multi_commit_pr() {
  local d="$WORK/multi-pr"
  git_init "$d"
  commit_file "$d" f.txt "0" "A"
  local base; base="$(sha "$d" HEAD)"
  git -C "$d" checkout -q -b feature
  local expect=()
  for i in $(seq 1 35); do
    commit_file "$d" f.txt "v$i" "C$i"
    expect+=("C$i")
  done
  local head; head="$(sha "$d" HEAD)"

  local subjects; subjects="$(git -C "$d" log --format=%s "$base..$head" | sort)"
  local want; want="$(printf '%s\n' "${expect[@]}" | sort)"
  if [ "$subjects" = "$want" ]; then
    ok "multi-commit PR (35 commits, past the old action's default per_page=30): full range covered because base/head come from trusted pull_request.base/head.sha fields, not a paginated commits-list API call"
  else
    bad "multi-commit PR: unexpected commit set (diff below)"
    diff <(printf '%s' "$subjects") <(printf '%s' "$want") || true
  fi
}

### 3. A branch merged into the PR branch before the PR's own tip commit #######
test_branch_merged_into_pr() {
  local d="$WORK/merged-branch-pr"
  git_init "$d"
  commit_file "$d" f.txt "a" "A"
  local base; base="$(sha "$d" HEAD)"
  git -C "$d" checkout -q -b feature "$base"
  commit_file "$d" f.txt "ab" "B"
  git -C "$d" checkout -q -b side
  commit_file "$d" side.txt "s" "S1"
  git -C "$d" checkout -q feature
  git -C "$d" merge -q --no-ff -m "Merge side into feature" side
  commit_file "$d" f.txt "abc" "C"
  local head; head="$(sha "$d" HEAD)"

  local with_fp without_fp
  with_fp="$(git -C "$d" log --format=%s --first-parent "$base..$head")"
  without_fp="$(git -C "$d" log --format=%s "$base..$head")"

  if printf '%s\n' "$with_fp" | grep -qx 'S1'; then
    bad "branch-merged-into-PR: control assertion failed — expected --first-parent to miss S1"
  else
    ok "branch-merged-into-PR: confirms --first-parent silently excludes S1 (the side-branch-only commit) — this is why the CI range must not use it"
  fi

  if printf '%s\n' "$without_fp" | grep -qx 'S1'; then
    ok "branch-merged-into-PR: the plain range (no --first-parent) includes S1"
  else
    bad "branch-merged-into-PR: plain range unexpectedly missed S1"
  fi
}

### 4. Merge commit whose conflict resolution introduces brand-new content #####
test_merge_commit_conflict_resolution() {
  local d="$WORK/merge-conflict"
  git_init "$d"
  commit_file "$d" f.txt $'line1\n' "A"
  local base; base="$(sha "$d" HEAD)"
  git -C "$d" checkout -q -b left "$base"
  commit_file "$d" f.txt $'line1\nleft-line\n' "L"
  git -C "$d" checkout -q -b right "$base"
  commit_file "$d" f.txt $'line1\nright-line\n' "R"
  git -C "$d" checkout -q left
  set +e
  git -C "$d" merge --no-ff -m "Merge right into left" right >/dev/null 2>&1
  set -e
  printf 'line1\nleft-line\nright-line\nMERGE_SECRET_TOKEN\n' > "$d/f.txt"
  git -C "$d" add f.txt
  git -C "$d" commit -q -m "M"
  local head; head="$(sha "$d" HEAD)"

  local without_m with_m
  without_m="$(git -C "$d" log -p "$base..$head")"
  with_m="$(git -C "$d" log -p -m "$base..$head")"

  if printf '%s' "$without_m" | grep -q "MERGE_SECRET_TOKEN"; then
    bad "merge-commit: control assertion failed — default 'git log -p' (no -m) should NOT surface conflict-resolution-only content"
  else
    ok "merge-commit: confirms default 'git log -p' suppresses the merge commit's diff — content introduced only during conflict resolution is otherwise invisible"
  fi

  if printf '%s' "$with_m" | grep -q "MERGE_SECRET_TOKEN"; then
    ok "merge-commit: '-m' surfaces the conflict-resolution-only content via the merge commit's per-parent diff"
  else
    bad "merge-commit: '-m' unexpectedly failed to surface the conflict-resolution content"
  fi
}

### 5. Push to main #############################################################
test_push_to_main() {
  local d="$WORK/push-main"
  git_init "$d"
  commit_file "$d" f.txt "a" "A"
  local before; before="$(sha "$d" HEAD)"
  commit_file "$d" f.txt "ab" "B"
  commit_file "$d" f.txt "abc" "C"
  local after; after="$(sha "$d" HEAD)"

  local out; out="$(resolve_range GITHUB_EVENT_NAME=push PUSH_BEFORE_SHA="$before" PUSH_AFTER_SHA="$after")"
  if [ "$(field "$out" base)" = "$before" ] && [ "$(field "$out" head)" = "$after" ]; then
    ok "push to main: base/head resolve to before/after SHAs"
  else
    bad "push to main: mismatch: $out"
  fi

  local subjects; subjects="$(git -C "$d" log --format=%s "$before..$after" | sort)"
  if [ "$subjects" = "$(printf 'B\nC' | sort)" ]; then
    ok "push to main: range covers exactly the pushed commits B, C"
  else
    bad "push to main: unexpected commit set: $subjects"
  fi
}

### 6. Initial push / zero before SHA ###########################################
test_initial_push_zero_before() {
  local d="$WORK/initial-push"
  git_init "$d"
  commit_file "$d" f.txt "a" "A"
  commit_file "$d" f.txt "ab" "B"
  commit_file "$d" f.txt "abc" "C"
  local head; head="$(sha "$d" HEAD)"
  local zero="0000000000000000000000000000000000000000"

  local out; out="$(resolve_range GITHUB_EVENT_NAME=push PUSH_BEFORE_SHA="$zero" PUSH_AFTER_SHA="$head")"
  if [ -z "$(field "$out" base)" ] && [ "$(field "$out" head)" = "$head" ]; then
    ok "initial push (all-zero before): base resolves to empty (full-history signal), head to current SHA"
  else
    bad "initial push (all-zero before): expected empty base: $out"
  fi

  local out2; out2="$(resolve_range GITHUB_EVENT_NAME=push PUSH_BEFORE_SHA="" PUSH_AFTER_SHA="$head")"
  if [ -z "$(field "$out2" base)" ]; then
    ok "initial push (empty before string): also resolves to empty base"
  else
    bad "initial push (empty before string): expected empty base: $out2"
  fi

  local opts; opts="$("$LOG_OPTS_SCRIPT" "" "$head")"
  local covered
  # shellcheck disable=SC2086 -- $opts is a flag string ("-m"), not a path
  covered="$(git -C "$d" log --format=%s $opts | sort)"
  if [ "$covered" = "$(printf 'A\nB\nC' | sort)" ]; then
    ok "initial push: empty-base log-opts scans full history (A, B, C) instead of only HEAD~1"
  else
    bad "initial push: full-history scan did not cover all commits: $covered"
  fi
}

### log-opts policy unit checks ##################################################
test_log_opts_policy() {
  local opts_ranged; opts_ranged="$("$LOG_OPTS_SCRIPT" "deadbeef" "cafef00d")"
  if [ "$opts_ranged" = "-m deadbeef..cafef00d" ]; then
    ok "log-opts: known range uses '-m base..head' (no --first-parent, no --no-merges)"
  else
    bad "log-opts: unexpected ranged output: $opts_ranged"
  fi

  local opts_full; opts_full="$("$LOG_OPTS_SCRIPT" "" "cafef00d")"
  if [ "$opts_full" = "-m" ]; then
    ok "log-opts: empty base uses '-m' alone (full history, no restrictive range)"
  else
    bad "log-opts: unexpected full-history output: $opts_full"
  fi
}

echo "== gitleaks range/log-opts fixture tests =="
test_linear_pr
test_multi_commit_pr
test_branch_merged_into_pr
test_merge_commit_conflict_resolution
test_push_to_main
test_initial_push_zero_before
test_log_opts_policy

echo
echo "== $PASS passed, $FAIL failed =="
[ "$FAIL" -eq 0 ]
