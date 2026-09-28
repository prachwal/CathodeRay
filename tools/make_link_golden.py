#!/usr/bin/env python3
"""Generuje trwałe wzorce testu linkowania (.include) w tests/CathodeRay.Tests/Link/.

Dla każdego wariantu CPU dzieli program golden na main.s + partN.inc (etykiety
i odwołania w przód/tył przekraczają granice plików), składa referencją
(ca65/ld65, z80asm — które też same rozwijają include'y) i zapisuje expected.bin.
Weryfikuje, że nasz asembler daje identyczne bajty (LinkTests.cs sprawdza to na stałe).

Uruchamiać z katalogu repo: python3 tools/make_link_golden.py
Wymaga ca65 i ld65 w PATH oraz z88dk w /opt/z88dk-2.4 (jak make_asm_golden.py).
"""

import os
import pathlib
import subprocess
import sys

REPO = pathlib.Path(__file__).resolve().parent.parent
ASM = REPO / "tests" / "CathodeRay.Tests" / "Asm"
OUT = REPO / "tests" / "CathodeRay.Tests" / "Link"
CFG = "MEMORY { M: start=$0000, size=$10000, file=%O; }\nSEGMENTS { CODE: load=M, type=rw; }\n"

# (katalog, cpu, składnia, program golden, ref-tool, dyrektywa, cięcia, zagnieżdżenie)
CASES = [
    (
        "6502",
        "6502",
        None,
        "6502/program.s",
        ("ca65", "6502"),
        ".include",
        [4, 12, 20],
        False,
    ),
    (
        "6502-mos",
        "6502",
        "mos",
        "6502/program_mos.s",
        None,
        ".INCLUDE",
        [4, 12, 20],
        False,
    ),
    (
        "6502x",
        "6502x",
        None,
        "6502/program.s",
        ("ca65", "6502X"),
        ".include",
        [4, 12, 20],
        False,
    ),
    (
        "65c02",
        "65c02",
        None,
        "65c02/program.s",
        ("ca65", "65C02"),
        ".include",
        [3, 9, 15],
        False,
    ),
    (
        "z80",
        "z80",
        None,
        "z80/program.s",
        ("z80asm", "z80_strict"),
        "INCLUDE",
        [5, 14, 24],
        False,
    ),
    (
        "z80u",
        "z80u",
        None,
        "z80/program.s",
        ("z80asm", "z80"),
        "INCLUDE",
        [5, 14, 24],
        False,
    ),
    (
        "8080",
        "8080",
        None,
        "8080/opcodes.s",
        ("z80asm", "8080"),
        "INCLUDE",
        [1, 25, 60],
        False,
    ),
    (
        "6502-nested",
        "6502",
        None,
        "6502/program.s",
        ("ca65", "6502"),
        ".include",
        [4, 12, 20],
        True,
    ),
]

STUB_MAIN = 'LDI 1\n.include "defs.inc"\nADD 2\nSTA $2001\nHLT\n'
STUB_DEFS = "LDI 9\nSTA $2000\n"
STUB_FLAT = "LDI 1\nLDI 9\nSTA $2000\nADD 2\nSTA $2001\nHLT\n"


def run(cmd, cwd, env=None):
    return subprocess.run(cmd, cwd=cwd, capture_output=True, text=True, env=env)


def ref_ca65(work, cpu):
    r = run(["ca65", "--cpu", cpu, "main.s", "-o", "p.o"], work)
    if r.returncode != 0:
        return None
    (work / "raw.cfg").write_text(CFG)
    r = run(["ld65", "-C", "raw.cfg", "p.o", "-o", "ref.bin"], work)
    return (work / "ref.bin").read_bytes() if r.returncode == 0 else None


def ref_z80asm(work, mode):
    env = dict(
        os.environ,
        PATH="/opt/z88dk-2.4/bin:" + os.environ["PATH"],
        ZCCCFG="/opt/z88dk-2.4/lib/config",
    )
    r = run(["z80asm", f"-m{mode}", "-b", "main.s"], work, env=env)
    out = work / "main.bin"
    return out.read_bytes() if r.returncode == 0 and out.exists() else None


def write_case(name, cpu, syntax, src, ref, directive, cuts, nested):
    work = OUT / name
    work.mkdir(parents=True, exist_ok=True)
    for f in work.glob("*"):
        if f.is_file() and f.name != "README.md":
            f.unlink()
    lines = (ASM / src).read_text().splitlines()
    if nested:
        (work / "sub").mkdir(exist_ok=True)
        (work / "sub" / "part1.inc").write_text(
            "\n".join(lines[cuts[1] : cuts[2]]) + "\n"
        )
        (work / "part0.inc").write_text(
            "\n".join(lines[cuts[0] : cuts[1]]) + f'\n{directive} "sub/part1.inc"\n'
        )
        main = lines[: cuts[0]] + [f'{directive} "part0.inc"'] + lines[cuts[2] :]
    else:
        main = list(lines[: cuts[0]])
        for i in range(len(cuts) - 1):
            (work / f"part{i}.inc").write_text(
                "\n".join(lines[cuts[i] : cuts[i + 1]]) + "\n"
            )
            main.append(f'{directive} "part{i}.inc"')
        main += lines[cuts[-1] :]
    (work / "main.s").write_text("\n".join(main) + "\n")
    if ref is None:
        # brak referencji dla składni (mos): wzorzec to złoty .bin programu
        golden = ASM / src.replace("_mos.s", ".bin")
        (work / "expected.bin").write_bytes(golden.read_bytes())
        print(f"{name}: wzorzec ze złotego {golden.parent.name}/{golden.name}")
        return
    kind, arg = ref
    binary = ref_ca65(work, arg) if kind == "ca65" else ref_z80asm(work, arg)
    if binary is None:
        raise SystemExit(f"referencja odrzuca {name}/main.s")
    (work / "expected.bin").write_bytes(binary)
    for f in work.glob("*"):
        if (
            f.is_file()
            and f.name not in ("main.s", "expected.bin")
            and f.suffix != ".inc"
        ):
            f.unlink()
    for f in (work / "sub").glob("*") if (work / "sub").is_dir() else []:
        if f.is_file() and f.suffix != ".inc":
            f.unlink()
    print(f"{name}: expected.bin ({len(binary)} B) z {'/'.join(ref)}")


def write_stub():
    work = OUT / "stub"
    work.mkdir(parents=True, exist_ok=True)
    (work / "defs.inc").write_text(STUB_DEFS)
    (work / "main.s").write_text(STUB_MAIN)
    (work / "flat.s").write_text(STUB_FLAT)
    # expected.bin złoży LinkTests z flat.s (brak referencji dla stuba)
    print("stub: main.s + defs.inc + flat.s (wzorzec z flat.s)")


def main() -> None:
    for name, cpu, syntax, src, ref, directive, cuts, nested in CASES:
        write_case(name, cpu, syntax, src, ref, directive, cuts, nested)
    write_stub()


if __name__ == "__main__":
    main()
