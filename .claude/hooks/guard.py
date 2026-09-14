#!/usr/bin/env python3
"""Repo-level PreToolUse defense-in-depth guard for Bash commands.

Native sandbox + settings.json permissions remain the primary safety
boundary; this is an additional, repo-local net so the same protection
travels with the repo regardless of whose personal ~/.claude config is
active (fresh clone, teammate, CI-driven agent). Mirrors the JSON
hook contract already proven in this environment by
~/.claude/hooks/scripts/guard_git_push.py: read tool_input.command from
stdin, emit {} to stay silent (defer to normal permission handling) or
hookSpecificOutput.permissionDecision in {"ask","deny"} with a reason.

Intentionally not a shell parser (see AGENTS.md / setup notes) — regexes
tolerate simple `;`, `&&`, `||` chaining, nothing more.
"""
import json
import re
import sys

BOUNDARY = r"(^|[;&|]\s*)"

# (name, compiled pattern, decision, reason)
RULES = [
    (
        "rm -rf",
        re.compile(BOUNDARY + r"rm\s+(-\w*r\w*f\w*|-\w*f\w*r\w*)\b"),
        "deny",
        "Recursive forced delete is blocked by repo policy — do it manually if truly intended.",
    ),
    (
        "git force-push / hard reset / forced clean",
        re.compile(
            r"git\s+push\b[^;&|]*(--force\b|--force-with-lease\b|(^|\s)-f(\s|$))"
            r"|git\s+reset\s+--hard\b"
            r"|git\s+clean\s+(-\w*f\w*d?\w*|-\w*d\w*f\w*)\b"
        ),
        "ask",
        "History-rewriting or hard-clean git operation — confirm before proceeding.",
    ),
    (
        "destructive SQL",
        re.compile(r"\b(DROP\s+(DATABASE|SCHEMA|TABLE)|TRUNCATE\s+TABLE)\b", re.IGNORECASE),
        "ask",
        "Destructive SQL statement detected — confirm target and environment before running.",
    ),
    (
        "dotnet ef database drop",
        re.compile(r"dotnet\s+(tool\s+run\s+)?dotnet-ef\s+database\s+drop|dotnet\s+ef\s+database\s+drop"),
        "ask",
        "Dropping the EF Core-managed database — confirm this isn't the shared/dev database by mistake.",
    ),
    (
        "infrastructure mutation",
        re.compile(
            r"terraform\s+destroy"
            r"|kubectl\s+delete\b"
            r"|docker\s+volume\s+rm\b"
            r"|docker\s+system\s+prune\b[^;&|]*-a\b"
        ),
        "ask",
        "Infrastructure-mutating command — confirm scope/target before proceeding.",
    ),
    (
        "credential file access",
        re.compile(
            r"\b(cat|less|more|head|tail|grep)\b[^;&|]*"
            r"(\.env(?!\.example)\b|id_rsa\b|\.pem\b|credentials\b|\.pfx\b)"
        ),
        "ask",
        "Command reads a file that looks like a secret/credential — confirm this is intentional.",
    ),
]


def main():
    try:
        data = json.load(sys.stdin)
    except Exception:
        print(json.dumps({}))
        return

    command = data.get("tool_input", {}).get("command", "")
    if not command:
        print(json.dumps({}))
        return

    for _name, pattern, decision, reason in RULES:
        if pattern.search(command):
            print(json.dumps({
                "hookSpecificOutput": {
                    "hookEventName": "PreToolUse",
                    "permissionDecision": decision,
                    "permissionDecisionReason": reason,
                }
            }))
            return

    print(json.dumps({}))


if __name__ == "__main__":
    main()
