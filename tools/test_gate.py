#!/usr/bin/env python3
"""Jedyny runner bramek: testy zmian, smoke/fuzz, goldeny i pełny przebieg."""

from __future__ import annotations

import argparse
import os
import re
import subprocess
import sys
import tempfile
import time
from concurrent.futures import ThreadPoolExecutor, as_completed
from dataclasses import dataclass, field
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
PROJECT = "tests/CathodeRay.Tests/CathodeRay.Tests.csproj"
RUNNERS_SOURCE = (ROOT / "tests/CathodeRay.Tests/Runners.cs").read_text()
TARGETS_MATCH = re.search(
    r"public static bool Has\(string target\).*?target is(?P<targets>[^;]+);",
    RUNNERS_SOURCE,
)
if TARGETS_MATCH is None:
    raise RuntimeError("Nie można odczytać celów z Runners.Has.")
TARGETS = tuple(re.findall(r'"([^"]+)"', TARGETS_MATCH["targets"]))
MATRIX_TESTS = ("TargetMatrixTests", "VRegMatrixTests")
FUZZ_TESTS = {"leaf": "LeafFuzzTests", "recursion": "RecursionFuzzTests"}


@dataclass
class Selection:
    classes: set[str] = field(default_factory=set)
    matrix_classes: set[str] = field(default_factory=set)
    matrix_targets: set[str] = field(default_factory=set)
    all_matrix_targets: bool = False
    ir_targets: set[str] = field(default_factory=set)
    fuzz: set[str] = field(default_factory=set)
    python_tests: bool = False
    full_fallback: bool = False


def changed_files(base: str) -> list[str]:
    merge_base = subprocess.run(
        ["git", "merge-base", "HEAD", base],
        cwd=ROOT,
        capture_output=True,
        text=True,
        check=True,
    ).stdout.strip()
    tracked = subprocess.run(
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
    return sorted(set(tracked) | set(untracked))


def select_changes(paths: list[str]) -> Selection:
    selection = Selection()
    for path in paths:
        if path in {
            "tools/test_gate.py",
            "tools/test_gate_test.py",
            "tools/affected_tests.py",
        }:
            selection.python_tests = True
            continue
        if (
            path == "tools/leaf_fuzz.py"
            or path == "tests/CathodeRay.Tests/LeafFuzzTests.cs"
        ):
            selection.fuzz.add("leaf")
            continue
        if (
            path == "tools/recursion_fuzz.py"
            or path == "tests/CathodeRay.Tests/RecursionFuzzTests.cs"
        ):
            selection.fuzz.add("recursion")
            continue
        if path in {"tests/CathodeRay.Tests/target-sizes.txt", "docs/target-sizes.md"}:
            selection.classes.add("TargetSizeTests")
            continue
        if path in {"tests/CathodeRay.Tests/vreg-sizes.txt", "docs/vreg-sizes.md"}:
            selection.classes.add("VRegSizeTests")
            continue
        if path == "docs/minic.md":
            selection.classes.add("MiniCDocsTests")
            continue
        if path.startswith("docs/") or path in {"AGENTS.md", "opencode.json"}:
            continue
        if path.startswith("tests/CathodeRay.Tests/") and path.endswith("Tests.cs"):
            class_name = Path(path).stem
            if class_name == "IrConformanceTests":
                selection.ir_targets.update(TARGETS)
            elif class_name in MATRIX_TESTS:
                selection.matrix_classes.add(class_name)
                selection.all_matrix_targets = True
            else:
                selection.classes.add(class_name)
            continue
        if path == "tests/CathodeRay.Tests/IrProgram.cs":
            selection.classes.update({"RandomIrTests", "RegisterAllocatorTests"})
            selection.ir_targets.update(TARGETS)
            continue
        if path in {
            "tests/CathodeRay.Tests/TargetHarness.cs",
            "tests/CathodeRay.Tests/Runners.cs",
        }:
            selection.full_fallback = True
            continue
        if (
            path == "src/CathodeRay.C/M6809Isa.cs"
            or path == "src/CathodeRay.C/M6809Target.cs"
        ):
            selection.classes.update(
                {
                    "M6809TargetTests",
                    "AbiV2MatrixTests",
                    "AbiCliTests",
                    "CpuModelConsistencyTests",
                }
            )
            selection.matrix_classes.update(MATRIX_TESTS)
            selection.matrix_targets.add("6809")
            selection.ir_targets.add("6809")
            continue
        if path == "src/CathodeRay.C/M6800Isa.cs":
            selection.classes.add("CpuModelConsistencyTests")
            selection.matrix_classes.update(MATRIX_TESTS)
            selection.matrix_targets.update({"6800", "6809"})
            selection.ir_targets.update({"6800", "6809"})
            continue
        if path == "src/CathodeRay.C/Z80Isa.cs":
            selection.classes.add("CpuModelConsistencyTests")
            selection.matrix_classes.update(MATRIX_TESTS)
            selection.matrix_targets.update({"z80", "8080"})
            selection.ir_targets.update({"z80", "8080"})
            continue
        if path == "src/CathodeRay.C/Mos6502Isa.cs":
            selection.classes.add("CpuModelConsistencyTests")
            selection.matrix_classes.update(MATRIX_TESTS)
            selection.matrix_targets.update({"6502", "65c02", "nes", "6510"})
            selection.ir_targets.update({"6502", "65c02", "nes", "6510"})
            continue
        if path.startswith("src/CathodeRay.C/ByteSelector") or path in {
            "src/CathodeRay.C/CpuModels.cs",
            "src/CathodeRay.C/IRegArgs.cs",
        }:
            selection.classes.add("CpuModelConsistencyTests")
            selection.matrix_classes.update(MATRIX_TESTS)
            selection.all_matrix_targets = True
            selection.ir_targets.update(TARGETS)
            continue
        if path.startswith("src/") or path.startswith("tests/"):
            selection.full_fallback = True
            continue
        if path.startswith("tools/"):
            selection.full_fallback = True
            continue
        selection.full_fallback = True
    return selection


def filter_expression(selection: Selection) -> str | None:
    terms: list[str] = []
    if selection.classes:
        terms.append(
            "|".join(f"FullyQualifiedName~{name}" for name in sorted(selection.classes))
        )
    for name in sorted(selection.matrix_classes):
        targets = (
            TARGETS
            if selection.all_matrix_targets
            else tuple(sorted(selection.matrix_targets))
        )
        if selection.all_matrix_targets:
            terms.append(f"FullyQualifiedName~{name}")
        else:
            terms.append(
                "|".join(
                    f"FullyQualifiedName~{name}&DisplayName~{cpu}" for cpu in targets
                )
            )
    return "|".join(terms) or None


def clean_env() -> dict[str, str]:
    env = os.environ.copy()
    for key in ("CATHODERAY_IR_TARGET", "LEAF_FUZZ_DIR", "RECURSION_FUZZ_DIR"):
        env.pop(key, None)
    return env


def run(
    label: str,
    command: list[str],
    env: dict[str, str] | None = None,
    capture: bool = False,
) -> tuple[int, float, str]:
    print(f"\n== {label} ==", flush=True)
    started = time.monotonic()
    result = subprocess.run(
        command,
        cwd=ROOT,
        env=env,
        text=True,
        stdout=subprocess.PIPE if capture else None,
        stderr=subprocess.STDOUT if capture else None,
        check=False,
    )
    elapsed = time.monotonic() - started
    output = result.stdout or "" if capture else ""
    if capture and output:
        print(output, end="" if output.endswith("\n") else "\n", flush=True)
    print(
        f"== {label}: {'OK' if result.returncode == 0 else 'FAIL'} ({elapsed:.1f}s) ==",
        flush=True,
    )
    return result.returncode, elapsed, output


def dotnet_filter(expr: str, env: dict[str, str] | None = None) -> int:
    command = [
        "dotnet",
        "test",
        PROJECT,
        "--no-build",
        "--nologo",
        "-v",
        "q",
        "--filter",
        expr,
    ]
    return run(f"dotnet test [{expr}]", command, env)[0]


def run_shard(kind: str, target: str) -> tuple[str, str, int, float, str]:
    env = clean_env()
    if kind == "ir":
        env["CATHODERAY_IR_TARGET"] = target
        expression = "FullyQualifiedName~IrConformanceTests"
    else:
        selection = Selection(matrix_classes=set(MATRIX_TESTS), matrix_targets={target})
        expression = filter_expression(selection)
        assert expression is not None
    command = [
        "dotnet",
        "test",
        PROJECT,
        "--no-build",
        "--nologo",
        "-v",
        "q",
        "--filter",
        expression,
    ]
    started = time.monotonic()
    result = subprocess.run(
        command,
        cwd=ROOT,
        env=env,
        text=True,
        stdout=subprocess.PIPE,
        stderr=subprocess.STDOUT,
        check=False,
    )
    return (
        kind,
        target,
        result.returncode,
        time.monotonic() - started,
        result.stdout or "",
    )


def run_shards(shards: list[tuple[str, str]], jobs: int) -> int:
    if not shards:
        return 0
    print(
        f"\n== Testy shardy: {', '.join(f'{kind}/{target}' for kind, target in shards)}; równolegle: {min(jobs, len(shards))} ==",
        flush=True,
    )
    failed = False
    with ThreadPoolExecutor(max_workers=min(jobs, len(shards))) as pool:
        futures = [pool.submit(run_shard, kind, target) for kind, target in shards]
        for future in as_completed(futures):
            kind, target, code, elapsed, output = future.result()
            print(
                f"\n--- {kind} {target} ({elapsed:.1f}s): {'OK' if code == 0 else 'FAIL'} ---",
                flush=True,
            )
            if output:
                print(output, end="" if output.endswith("\n") else "\n", flush=True)
            failed |= code != 0
    return int(failed)


def run_ir_shards(targets: set[str], jobs: int) -> int:
    return run_shards([("ir", target) for target in sorted(targets)], jobs)


def run_full_shards(jobs: int) -> int:
    matrices = [target for target in TARGETS if target != "stub"]
    preferred = [
        ("ir", "stub"),
        ("matrix", "z80"),
        ("matrix", "nes"),
        ("matrix", "6809"),
    ]
    remaining = [("ir", target) for target in TARGETS if target != "stub"]
    remaining.extend(
        ("matrix", target)
        for target in matrices
        if target not in {"z80", "nes", "6809"}
    )
    return run_shards(preferred + remaining, jobs)


def run_fuzz(kind: str, count: int) -> int:
    directory = tempfile.mkdtemp(prefix=f"cathoderay-{kind}-fuzz-")
    script = "leaf_fuzz.py" if kind == "leaf" else "recursion_fuzz.py"
    variable = "LEAF_FUZZ_DIR" if kind == "leaf" else "RECURSION_FUZZ_DIR"
    test_class = FUZZ_TESTS[kind]
    if run(
        f"Generuj {kind} fuzz ({count})",
        [sys.executable, f"tools/{script}", "gen", str(count), directory],
    )[0]:
        return 1
    env = clean_env()
    env[variable] = directory
    return dotnet_filter(f"FullyQualifiedName~{test_class}", env)


def run_fuzz_set(kinds: set[str], count: int) -> int:
    for kind in sorted(kinds):
        if run_fuzz(kind, count):
            return 1
    return 0


def run_docs_checks() -> int:
    if run("Plan hygiene i linki", [sys.executable, "tools/plan.py", "hygiene"])[0]:
        return 1
    return run("git diff --check", ["git", "diff", "--check"])[0]


def run_build() -> int:
    return run("Build -warnaserror", ["dotnet", "build", "-warnaserror"])[0]


def run_goldens() -> int:
    if run_build():
        return 1
    return dotnet_filter(
        "FullyQualifiedName~TargetSizeTests|FullyQualifiedName~VRegSizeTests",
        clean_env(),
    )


def run_full(fuzz_count: int, jobs: int) -> int:
    if run_build() or run_fuzz_set(set(FUZZ_TESTS), fuzz_count):
        return 1
    if run("Testy selektora", [sys.executable, "tools/test_gate_test.py"])[0]:
        return 1
    if run_full_shards(jobs):
        return 1
    non_ir = "FullyQualifiedName!~IrConformanceTests&FullyQualifiedName!~TargetMatrixTests&FullyQualifiedName!~VRegMatrixTests&FullyQualifiedName!~LeafFuzzTests&FullyQualifiedName!~RecursionFuzzTests"
    if dotnet_filter(non_ir, clean_env()):
        return 1
    return run_docs_checks()


def run_changed(base: str, jobs: int, dry_run: bool) -> int:
    paths = changed_files(base)
    selection = select_changes(paths)
    print(f"Zmiany względem {base}: {len(paths)} plików")
    for path in paths:
        print(f"  {path}")
    if selection.full_fallback:
        print("Nieznana ścieżka kodu — bezpieczny fallback do pełnego flow.")
        if dry_run:
            return 0
        return run_full(60, jobs)

    expr = filter_expression(selection)
    if dry_run:
        print(f"Klasy: {', '.join(sorted(selection.classes)) or '—'}")
        print(f"Macierze: {', '.join(sorted(selection.matrix_classes)) or '—'}")
        print(f"IR cele: {', '.join(sorted(selection.ir_targets)) or '—'}")
        print(f"Fuzz smoke: {', '.join(sorted(selection.fuzz)) or '—'}")
        print(f"Filtr dotnet: {expr or '—'}")
        return 0

    needs_dotnet = (
        expr is not None or bool(selection.ir_targets) or bool(selection.fuzz)
    )
    if needs_dotnet and run_build():
        return 1
    if (
        selection.python_tests
        and run("Testy selektora", [sys.executable, "tools/test_gate_test.py"])[0]
    ):
        return 1
    if expr and dotnet_filter(expr, clean_env()):
        return 1
    if selection.ir_targets and run_ir_shards(selection.ir_targets, jobs):
        return 1
    if selection.fuzz and run_fuzz_set(selection.fuzz, 5):
        return 1
    if not needs_dotnet:
        return run_docs_checks()
    return 0


def parser() -> argparse.ArgumentParser:
    result = argparse.ArgumentParser(description=__doc__)
    sub = result.add_subparsers(dest="mode", required=True)
    changed = sub.add_parser("changed", help="uruchom testy dobrane do diffu")
    changed.add_argument("--base", default="main")
    changed.add_argument("--jobs", type=int, default=min(4, os.cpu_count() or 1))
    changed.add_argument("--dry-run", action="store_true")
    smoke = sub.add_parser("smoke", help="krótki, 5-seedowy fuzz smoke obu generatorów")
    smoke.add_argument("--count", type=int, default=5)
    fuzz = sub.add_parser("fuzz", help="pełne różnicowe fuzzing bez pełnej suity")
    fuzz.add_argument("--count", type=int, default=60)
    golden = sub.add_parser("goldens", help="sprawdź goldeny rozmiarów Cell i VReg")
    full = sub.add_parser(
        "full", help="końcowa bramka: build, fuzz, pełna suita sharded, hygiene"
    )
    full.add_argument("--fuzz-count", type=int, default=60)
    full.add_argument("--jobs", type=int, default=min(4, os.cpu_count() or 1))
    return result


def main(argv: list[str] | None = None) -> int:
    args = parser().parse_args(argv)
    if args.mode == "changed":
        return run_changed(args.base, max(1, args.jobs), args.dry_run)
    if args.mode == "smoke":
        return run_build() or run_fuzz_set(set(FUZZ_TESTS), args.count)
    if args.mode == "fuzz":
        return run_build() or run_fuzz_set(set(FUZZ_TESTS), args.count)
    if args.mode == "goldens":
        return run_goldens()
    if args.mode == "full":
        return run_full(args.fuzz_count, max(1, args.jobs))
    return 2


if __name__ == "__main__":
    raise SystemExit(main())
