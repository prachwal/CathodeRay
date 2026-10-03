"""Dobiera testy do zmian w drzewie (pre-commit, zamiast calej suity).

Uzycie: python3 tools/affected_tests.py [--base main]
Drukuje gotowa komende dotnet test z filtrem. Bez dopasowania -> pelna suita.
"""

import subprocess
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent

# (prefix sciezki, filtry FullyQualifiedName~Klasa). Kolejnosc ma znaczenie.
RULES = [
    ("tests/CathodeRay.Tests/target-sizes.txt", ["TargetSizeTests"]),
    ("tests/CathodeRay.Tests/vreg-sizes.txt", ["VRegSizeTests"]),
    ("tests/CathodeRay.Tests/v2-sizes.txt", ["AbiV2SizeTests"]),
    ("docs/minic.md", ["MiniCDocsTests"]),
    (
        "src/CathodeRay.Assembler/",
        [
            "Asm",
            "Link",
            "Cond",
            "Locals",
            "Macros",
            "Segments",
            "Modules",
            "Scopes",
            "Longtail",
            "Errors",
            "AlignHex",
        ],
    ),
    ("src/CathodeRay.Cli/", ["Cli", "CcRun"]),
    ("src/CathodeRay.C/Z80", ["Z80", "TargetMatrix", "IrConformance", "TargetSize"]),
    (
        "src/CathodeRay.C/Intel8080",
        ["Intel8080", "8080", "TargetMatrix", "IrConformance", "TargetSize"],
    ),
    (
        "src/CathodeRay.C/Mos6502",
        ["Mos6502", "6502", "TargetMatrix", "IrConformance", "TargetSize"],
    ),
    (
        "src/CathodeRay.C/M6800",
        ["Mc6800", "6800", "TargetMatrix", "IrConformance", "TargetSize"],
    ),
    (
        "src/CathodeRay.C/Byte",
        ["TargetMatrix", "IrConformance", "TargetSize", "TailCall", "RecursionFrame"],
    ),
    ("src/CathodeRay.C/VReg", ["VReg", "TargetMatrix", "TargetSize"]),
    (
        "src/CathodeRay.C/Lowering",
        ["TargetMatrix", "IrConformance", "RecursionFrame", "FrameModel"],
    ),
    ("src/CathodeRay.C/Ir", ["IrConformance", "IrGate", "TargetMatrix"]),
    ("src/CathodeRay.C/Cpu", ["CpuModel", "TargetMatrix"]),
    ("src/CathodeRay.C/", ["CCodegen", "TargetMatrix", "IrConformance", "MiniCDocs"]),
    ("src/CathodeRay/", ["Stub"]),
]

# Pliki niewymagajace testow (docs, plany, goldeny wejsciowe).
SKIP_PREFIXES = (
    "docs/plans/",
    "docs/technical/",
    "docs/codegen/",
    "docs/ir-vreg-abi/",
    "docs/language/",
    "docs/assembler/",
    "docs/archive/",
    "docs/README.md",
    "tools/",
    ".opencode/",
)


def changed_files(base: str) -> list[str]:
    """Zmienione + nowe pliki wzgledem bazy (merge-base z HEAD)."""
    merge_base = subprocess.run(
        ["git", "merge-base", "HEAD", base],
        cwd=ROOT,
        capture_output=True,
        text=True,
        check=True,
    ).stdout.strip()
    diff = subprocess.run(
        ["git", "diff", "--name-only", merge_base, "--", "."],
        cwd=ROOT,
        capture_output=True,
        text=True,
        check=True,
    ).stdout.split()
    untracked = subprocess.run(
        ["git", "ls-files", "--others", "--exclude-standard"],
        cwd=ROOT,
        capture_output=True,
        text=True,
        check=True,
    ).stdout.split()
    return sorted(set(diff) | set(untracked))


def test_file_class(path: str) -> str | None:
    """tests/.../FooTests.cs -> FooTests (tylko jesli klasa istnieje w pliku)."""
    name = Path(path).name
    if path.startswith("tests/") and name.endswith("Tests.cs"):
        return name[:-3]
    return None


def main() -> int:
    base = sys.argv[sys.argv.index("--base") + 1] if "--base" in sys.argv else "main"
    filters: list[str] = []
    covered = True
    for path in changed_files(base):
        cls = test_file_class(path)
        if cls is not None:
            filters.append(cls)
            continue
        if path.startswith(SKIP_PREFIXES):
            continue
        for prefix, names in RULES:
            if path.startswith(prefix):
                filters.extend(names)
                break
        else:
            if path.startswith("docs/") or path == "AGENTS.md":
                continue
            covered = False
    filters = sorted(set(filters))
    if not filters:
        print("# brak zmian wymagajacych testow (docs/plany); bramka: build + hygiene")
        return 0
    if not covered:
        print("# zmiana poza regulami -> pelna suita")
        print("dotnet test --nologo -v q")
        return 0
    expr = "|".join(f"FullyQualifiedName~{f}" for f in filters)
    print(
        f'dotnet test tests/CathodeRay.Tests/CathodeRay.Tests.csproj --filter "{expr}" --nologo -v q'
    )
    return 0


if __name__ == "__main__":
    sys.exit(main())
