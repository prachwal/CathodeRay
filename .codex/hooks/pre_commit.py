"""Uruchamia wymagane kontrole przed commitem wykonywanym przez Codexa."""

import json
import re
import subprocess
import sys
import tempfile
import xml.etree.ElementTree as ET
from pathlib import Path


PRE_COMMIT_FILTER = "&".join(
    f"FullyQualifiedName!~{name}"
    for name in (
        "AbiV2MatrixTests",
        "AbiV2SizeTests",
        "IrGateTests",
        "TargetMatrixTests",
        "TargetSizeTests",
        "VRegSizeTests",
    )
)


def report_slowest_tests(results: Path) -> None:
    """Wypisuje dziesięć najdłuższych testów z plików TRX."""
    timings: list[tuple[float, str]] = []
    for trx in results.glob("**/*.trx"):
        try:
            root = ET.parse(trx).getroot()
        except ET.ParseError:
            continue
        for result in root.iter():
            if not result.tag.endswith("UnitTestResult"):
                continue
            duration = result.attrib.get("duration")
            if not duration:
                continue
            hours, minutes, seconds = duration.split(":")
            elapsed = int(hours) * 3600 + int(minutes) * 60 + float(seconds)
            timings.append((elapsed, result.attrib.get("testName", "(bez nazwy)")))

    if timings:
        print("Najdłuższe testy (czas / nazwa):", file=sys.stderr)
        for elapsed, name in sorted(timings, reverse=True)[:10]:
            print(f"{elapsed:7.2f} s  {name}", file=sys.stderr)


def main() -> int:
    """Blokuje commit, gdy repozytoryjne bramki nie przechodzą."""
    payload = json.load(sys.stdin)
    command = payload.get("tool_input", {}).get("command", "")
    if not re.search(r"\bgit\s+(?:-C\s+\S+\s+)?commit\b", command):
        return 0

    root = Path(__file__).resolve().parents[2]
    checks = [["dotnet", "build", "-warnaserror"]]
    for check in checks:
        print(f"$ {' '.join(check)}", file=sys.stderr, flush=True)
        result = subprocess.run(check, cwd=root, check=False, stdout=sys.stderr)
        if result.returncode != 0:
            return deny(f"{' '.join(check)} zakończyło się kodem {result.returncode}.")

    with tempfile.TemporaryDirectory(prefix="cathoderay-precommit-") as temp:
        results = Path(temp)
        test = [
            "dotnet",
            "test",
            "tests/CathodeRay.Tests/CathodeRay.Tests.csproj",
            "--filter",
            PRE_COMMIT_FILTER,
            "--logger",
            "trx",
            "--results-directory",
            str(results),
        ]
        print(f"$ {' '.join(test)}", file=sys.stderr, flush=True)
        result = subprocess.run(test, cwd=root, check=False, stdout=sys.stderr)
        report_slowest_tests(results)
        if result.returncode != 0:
            return deny(f"{' '.join(test[:2])} zakończyło się kodem {result.returncode}.")

    check = ["python3", "tools/plan.py", "hygiene"]
    print(f"$ {' '.join(check)}", file=sys.stderr, flush=True)
    result = subprocess.run(check, cwd=root, check=False, stdout=sys.stderr)
    if result.returncode != 0:
        return deny(f"{' '.join(check)} zakończyło się kodem {result.returncode}.")

    print(
        json.dumps(
            {
                "hookSpecificOutput": {
                    "hookEventName": "PreToolUse",
                    "permissionDecision": "allow",
                    "permissionDecisionReason": "Build, suita pre-commit i hygiene przeszły.",
                }
            }
        )
    )
    return 0


def deny(reason: str) -> int:
    """Odrzuca narzędzie PreToolUse z podaniem przyczyny."""
    print(
        json.dumps(
            {
                "hookSpecificOutput": {
                    "hookEventName": "PreToolUse",
                    "permissionDecision": "deny",
                    "permissionDecisionReason": f"Commit zablokowany: {reason}",
                }
            }
        )
    )
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
