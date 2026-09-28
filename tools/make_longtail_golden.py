#!/usr/bin/env python3
"""Generuje wzorce long-tail w tests/CathodeRay.Tests/Longtail/.

Dla 6502 pisze main.s (.out/.warning/.assert/.define/.ifblank/.paramcount),
składa referencją ca65+ld65 i zapisuje expected.bin. Weryfikuje LongtailTests.cs.
Uwaga: ten build ca65 (V2.18 Ubuntu) odrzuca wieloparametrowe .macro i .define
funkcyjne — golden używa tylko konstrukcji akceptowanych przez referencję.
"""

import pathlib
import subprocess
import sys

REPO = pathlib.Path(__file__).resolve().parent.parent
OUT = REPO / "tests" / "CathodeRay.Tests" / "Longtail"
CFG = "MEMORY { M: start=$0000, size=$10000, file=%O; }\nSEGMENTS { CODE: load=M, type=rw; }\n"

CA65 = """; Long-tail porównywane z ca65: .out/.warning/.assert/.define/.ifnblank.
.define STEP 2
.define MSG "hi"
        .org $0600
        .out MSG
        .warning "golden"
        .assert STEP = 2, error, "step"
        .assert STEP = 3, warning, "silent"
        .macro one v
        .ifnblank v
        lda #v
        .endif
        .endmacro
        one STEP
        one
        .macro cnt v
        lda #.paramcount
        .endmacro
        cnt STEP
        rts
"""


def ca65(work):
    r = subprocess.run(
        ["ca65", "--cpu", "6502", "main.s", "-o", "p.o"],
        cwd=work,
        capture_output=True,
        text=True,
    )
    if r.returncode != 0:
        print(r.stderr[-2000:])
        return None
    (work / "raw.cfg").write_text(CFG)
    r = subprocess.run(
        ["ld65", "-C", "raw.cfg", "p.o", "-o", "ref.bin"],
        cwd=work,
        capture_output=True,
        text=True,
    )
    if r.returncode != 0:
        print(r.stderr[-1000:])
        return None
    return (work / "ref.bin").read_bytes()


def main() -> None:
    work = OUT / "6502"
    work.mkdir(parents=True, exist_ok=True)
    (work / "main.s").write_text(CA65)
    binary = ca65(work)
    if binary is None:
        raise SystemExit("referencja odrzuca Longtail/6502/main.s")
    (work / "expected.bin").write_bytes(binary)
    for f in work.glob("*"):
        if f.is_file() and f.name not in ("main.s", "expected.bin"):
            f.unlink()
    print(f"6502: expected.bin ({len(binary)} B)")


if __name__ == "__main__":
    main()
