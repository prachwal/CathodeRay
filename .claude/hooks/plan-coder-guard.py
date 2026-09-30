#!/usr/bin/env python3
"""PreToolUse guard dla subagenta plan-coder: twarde ramy (exit 2 = zablokuj, komunikat na stderr wraca do modelu)."""
import json
import os
import re
import sys

ROOT = os.path.realpath(os.environ.get("CLAUDE_PROJECT_DIR", os.getcwd()))
WRITABLE = ("src/CathodeRay.C/", "tests/CathodeRay.Tests/", "docs/", "samples/", "tools/")
PROTECTED = ("tests/CathodeRay.Tests/CcRun.cs", "tests/CathodeRay.Tests/Repo.cs", ".claude/", "docs/plans/")
# dozwolone polecenia (początek polecenia po usunięciu spacji)
BASH_ALLOW = [
    r"^dotnet (build|test) --nologo",
    r"^(UPDATE_IR_GATE|UPDATE_TARGET_SIZES)=1 dotnet test --nologo",
    r"^python3 tools/(compare|hotspots|plan)\.py",
    r"^git (status|diff|log|add|show)\b",
    r"^git commit -m ",
    r"^git branch --show-current$",
    r"^(ls|grep|cat|head|tail|wc|sed -n|awk|find|sort|uniq)\b",
    r"^dotnet src/CathodeRay\.Cli/bin/Debug/net10\.0/cathode\.dll cc ",
]
BASH_DENY = [r"\bgit (push|merge|rebase|reset|checkout|switch|stash|clean|restore|branch -[dDmM])", r"\brm\b", r"--no-verify", r"\bsudo\b",
             r"\bcurl\b|\bwget\b", r">\s*/(?!tmp/)", r"\bchmod\b", r"--force|-f\b.*\bgit\b"]


def deny(message):
    print(f"ZABLOKOWANE przez plan-coder-guard: {message}", file=sys.stderr)
    sys.exit(2)


def rel(path):
    full = os.path.realpath(path if os.path.isabs(path) else os.path.join(ROOT, path))
    if not full.startswith(ROOT + os.sep):
        deny(f"ścieżka poza repozytorium: {path}")
    return full[len(ROOT) + 1:]


data = json.load(sys.stdin)
tool = data.get("tool_name", "")
args = data.get("tool_input", {})
if tool in ("Edit", "Write"):
    path = rel(args.get("file_path", ""))
    if path.startswith(PROTECTED) and not path.startswith("docs/plans/"):
        deny(f"plik chroniony: {path}")
    if not path.startswith(WRITABLE) or path.startswith("docs/plans/"):
        deny(f"wolno edytować tylko {', '.join(WRITABLE)} (plany zmieniasz wyłącznie przez tools/plan.py): {path}")
elif tool == "Bash":
    command = args.get("command", "").strip()
    if any(sep in command for sep in ("&&", "||", ";", "|", "`", "$(", "\n")) and not command.startswith("("):
        # łańcuchy tylko dla prostych potoków do grep/head/tail/wc/sort/uniq
        parts = [p.strip() for p in re.split(r"\|", command)]
        if any(sep in command for sep in ("&&", "||", ";", "`", "$(", "\n")):
            deny("jedno polecenie na wywołanie (bez &&, ||, ;, podstawień); potoki tylko do grep/head/tail/wc/sort/uniq")
        if any(not re.match(r"^(grep|head|tail|wc|sort|uniq|awk)\b", p) for p in parts[1:]):
            deny("po potoku dozwolone tylko grep/head/tail/wc/sort/uniq/awk")
    first = command.split("|")[0].strip()
    for pattern in BASH_DENY:
        if re.search(pattern, first):
            deny(f"zabronione polecenie: {first}")
    if not any(re.match(pattern, first) for pattern in BASH_ALLOW):
        deny(f"polecenie spoza listy dozwolonych: {first}")
    if re.match(r"^git commit", first):
        import subprocess
        branch = subprocess.run(["git", "branch", "--show-current"], capture_output=True, text=True, cwd=ROOT).stdout.strip()
        if branch in ("main", "master"):
            deny("commit na main zabroniony; pracuj na gałęzi stub/c-performance")
sys.exit(0)
