#!/usr/bin/env python3
"""Adapt the shared Claude command policy to Codex's PreToolUse contract."""

import importlib.util
import json
import sys
from pathlib import Path


def load_shared_policy():
    policy_path = Path(__file__).resolve().parents[2] / ".claude" / "hooks" / "guard.py"
    specification = importlib.util.spec_from_file_location("claude_guard", policy_path)
    if specification is None or specification.loader is None:
        raise RuntimeError("Unable to load the shared repository command policy.")

    module = importlib.util.module_from_spec(specification)
    specification.loader.exec_module(module)
    return module


def main() -> None:
    try:
        event = json.load(sys.stdin)
        command = event.get("tool_input", {}).get("command", "")
        if not command:
            print("{}")
            return

        policy = load_shared_policy()
        for _name, pattern, _decision, reason in policy.RULES:
            if pattern.search(command):
                print(
                    json.dumps(
                        {
                            "hookSpecificOutput": {
                                "hookEventName": "PreToolUse",
                                "permissionDecision": "deny",
                                "permissionDecisionReason": (
                                    f"{reason} Codex does not support a PreToolUse ask decision; "
                                    "use an explicitly reviewed manual workflow instead."
                                ),
                            }
                        }
                    )
                )
                return
    except Exception as error:
        print(
            json.dumps(
                {
                    "hookSpecificOutput": {
                        "hookEventName": "PreToolUse",
                        "permissionDecision": "deny",
                        "permissionDecisionReason": f"Safety policy failed closed: {error}",
                    }
                }
            )
        )
        return

    print("{}")


if __name__ == "__main__":
    main()
