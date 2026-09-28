#!/usr/bin/env python3
"""Generuje wzorce wielomodułowe w tests/CathodeRay.Tests/Modules/.

Dla każdego CPU pisze moduły (nasza składnia GLOBAL/EXTERN + wariant referencji:
.import/.export dla ca65) i map.cfg, składa referencją (ca65+ld65, z80asm link
obiektów) i zapisuje expected.bin. Weryfikuje ModulesTests.cs.
"""

import os
import pathlib
import subprocess
import sys

REPO = pathlib.Path(__file__).resolve().parent.parent
OUT = REPO / "tests" / "CathodeRay.Tests" / "Modules"
CFG = "MEMORY { RAM: start=$BASE, size=$1000, file=%O, fill=yes; }\nSEGMENTS { CODE: load=RAM, type=rw; DATA: load=RAM, type=rw; }\n"

CA65_MAIN = """.import getval
.export main
.segment "CODE"
main:   lda getval
        sta result
        rts
.segment "DATA"
result: .byte 0
"""
CA65_LIB = """.export getval
.segment "DATA"
getval: .byte 42
"""
OURS_CA65 = """$EXTERN getval
$GLOBAL main
.segment "CODE"
main:   lda getval
        sta result
        rts
.segment "DATA"
result: .byte 0
"""
OURS_CA65_LIB = """$GLOBAL getval
.segment "DATA"
getval: .byte 42
"""

Z80_MAIN = """EXTERN getval
GLOBAL main
SEGMENT "CODE"
main:   LD A,(getval)
        LD (result),A
        RET
SEGMENT "DATA"
result: DEFS 1
"""
Z80_LIB = """GLOBAL getval
SEGMENT "DATA"
getval: DEFB 42
"""

I8080_MAIN = """EXTERN getval
GLOBAL main
SEGMENT "CODE"
main:   LDA getval
        STA result
        HLT
SEGMENT "DATA"
result: DS 1
"""
I8080_LIB = """GLOBAL getval
SEGMENT "DATA"
getval: DB 42
"""

REF_Z80_MAIN = """EXTERN getval
GLOBAL main
SECTION CODE
main:   LD A,(getval)
        LD (result),A
        RET
SECTION DATA
result: DEFS 1
"""
REF_Z80_LIB = """GLOBAL getval
SECTION DATA
getval: DEFB 42
"""
REF_8080_MAIN = """EXTERN getval
GLOBAL main
SECTION CODE
main:   LDA getval
        STA result
        HLT
SECTION DATA
result: DS 1
"""
REF_8080_LIB = """GLOBAL getval
SECTION DATA
getval: DB 42
"""


CASES = {
    "6502": (
        "6502",
        "ca65-6502",
        OURS_CA65,
        OURS_CA65_LIB,
        CA65_MAIN,
        CA65_LIB,
        0x0200,
    ),
    "6502x": (
        "6502x",
        "ca65-6502X",
        OURS_CA65,
        OURS_CA65_LIB,
        CA65_MAIN,
        CA65_LIB,
        0x0200,
    ),
    "65c02": (
        "65c02",
        "ca65-65C02",
        OURS_CA65,
        OURS_CA65_LIB,
        CA65_MAIN,
        CA65_LIB,
        0x0400,
    ),
    "z80": ("z80", "z80-strict", Z80_MAIN, Z80_LIB, REF_Z80_MAIN, REF_Z80_LIB, 0),
    "z80u": ("z80u", "z80", Z80_MAIN, Z80_LIB, REF_Z80_MAIN, REF_Z80_LIB, 0),
    "8080": ("8080", "8080", I8080_MAIN, I8080_LIB, REF_8080_MAIN, REF_8080_LIB, 0),
}


def ref_ca65(work, cpu, base):
    (work / "ref_main.s").write_text(CASES[cur][4])
    (work / "ref_lib.s").write_text(CASES[cur][5])
    for name in ("ref_main.s", "ref_lib.s"):
        r = subprocess.run(
            ["ca65", "--cpu", cpu, name, "-o", name.replace(".s", ".o")],
            cwd=work,
            capture_output=True,
            text=True,
        )
        if r.returncode != 0:
            print(r.stderr[-1000:])
            return None
    (work / "raw.cfg").write_text(CFG.replace("$BASE", f"${base:04X}"))
    r = subprocess.run(
        ["ld65", "-C", "raw.cfg", "ref_main.o", "ref_lib.o", "-o", "ref.bin"],
        cwd=work,
        capture_output=True,
        text=True,
    )
    if r.returncode != 0:
        print(r.stderr[-1000:])
        return None
    return (work / "ref.bin").read_bytes().rstrip(b"\x00")


def ref_z80(work, mode):
    (work / "ref_main.asm").write_text(CASES[cur][4])
    (work / "ref_lib.asm").write_text(CASES[cur][5])
    env = dict(
        os.environ,
        PATH="/opt/z88dk-2.4/bin:" + os.environ["PATH"],
        ZCCCFG="/opt/z88dk-2.4/lib/config",
    )
    for name in ("ref_main.asm", "ref_lib.asm"):
        r = subprocess.run(
            ["z80asm", f"-m{mode}", name],
            cwd=work,
            capture_output=True,
            text=True,
            env=env,
        )
        if r.returncode != 0:
            print(r.stderr[-1000:])
            return None
    r = subprocess.run(
        ["z80asm", "-b", "ref_main.o", "ref_lib.o"],
        cwd=work,
        capture_output=True,
        text=True,
        env=env,
    )
    if r.returncode != 0:
        print(r.stderr[-1000:])
        return None
    out = work / "ref_main.bin"
    return out.read_bytes() if out.exists() else None


def main() -> None:
    global cur
    for cur, (
        cpu,
        ref,
        ours_main,
        ours_lib,
        _ref_main,
        _ref_lib,
        base,
    ) in CASES.items():
        work = OUT / cur
        work.mkdir(parents=True, exist_ok=True)
        (work / "mod0.s").write_text(
            ours_main.replace("$EXTERN", ".extern").replace("$GLOBAL", ".global")
            if ref.startswith("ca65")
            else ours_main
        )
        (work / "mod1.s").write_text(
            ours_lib.replace("$GLOBAL", ".global")
            if ref.startswith("ca65")
            else ours_lib
        )
        binary = (
            ref_ca65(work, ref.split("-")[1], base)
            if ref.startswith("ca65")
            else ref_z80(
                work, {"z80-strict": "z80_strict", "z80": "z80", "8080": "8080"}[ref]
            )
        )
        if binary is None or len(binary) == 0 or binary[-1] == 0:
            raise SystemExit(f"referencja odrzuca Modules/{cur}")
        (work / "map.cfg").write_text(
            CFG.replace("$BASE", f"${base:04X}" if ref.startswith("ca65") else "$0000")
        )
        (work / "expected.bin").write_bytes(binary)
        for f in work.glob("*"):
            if f.is_file() and f.name not in (
                "mod0.s",
                "mod1.s",
                "map.cfg",
                "expected.bin",
            ):
                f.unlink()
        print(f"{cur}: expected.bin ({len(binary)} B)")


if __name__ == "__main__":
    main()
