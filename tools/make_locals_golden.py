#!/usr/bin/env python3
"""Generuje wzorce tanich etykiet (@) i .incbin w tests/CathodeRay.Tests/Locals/.

Dla każdego CPU z referencją pisze main.s (lokalne przekraczające zakresy,
forward-ref, .incbin/INCBIN) + data.bin, składa referencją (ca65/ld65, z80asm)
i zapisuje expected.bin. Weryfikuje LocalsTests.cs (odkrywanie po katalogu).
"""

import os
import pathlib
import subprocess
import sys

REPO = pathlib.Path(__file__).resolve().parent.parent
OUT = REPO / "tests" / "CathodeRay.Tests" / "Locals"
CFG = "MEMORY { M: start=$0000, size=$10000, file=%O; }\nSEGMENTS { CODE: load=M, type=rw; }\n"
DATA = bytes([0x42, 0x00, 0xFF, 0x10])

CA65 = """; Tanie etykiety i .incbin, porównane z ca65: zakresy, forward-ref w zakresie.
        .org $0600
aa:     lda #1
@x:     lda #2
bb:     lda #3
@x:     lda #4
        lda @x
        jmp @fwd
@fwd:   .incbin "data.bin"
        rts
"""

CA65X = """; 6502X: lokalne + .incbin (LAX w drugiej gałęzi zakresu).
        .org $0600
aa:     lax $12
@x:     nop
bb:     lax $13
@x:     nop
        lda @x
        .incbin "data.bin"
        rts
"""

CA65C02 = """; 65C02: lokalne + .incbin (STZ w zakresie).
        .org $0800
aa:     stz $20
@x:     nop
bb:     stz $21
@x:     nop
        lda @x
        .incbin "data.bin"
        rts
"""

Z80 = """; Z80: lokalne i INCBIN, porównane z z80asm -mz80_strict.
        ORG 8000H
aa:     LD A,1
@x:     LD A,2
bb:     LD A,3
@x:     LD A,4
        LD A,(@x)
        JR @fwd
@fwd:   INCBIN "data.bin"
        RET
"""

Z80U = """; z80u: lokalne i INCBIN (IXL w zakresie).
        ORG 8000H
aa:     LD A,IXL
@x:     NOP
bb:     LD A,IXH
@x:     NOP
        LD A,(@x)
        INCBIN "data.bin"
        RET
"""

I8080 = """; 8080: lokalne i INCBIN, porównane z z80asm -m8080.
        ORG 100H
aa:     MVI A,1
@x:     MVI A,2
bb:     MVI A,3
@x:     MVI A,4
        LDA @x
        INCBIN "data.bin"
        HLT
"""


def ca65(work, cpu):
    r = subprocess.run(
        ["ca65", "--cpu", cpu, "main.s", "-o", "p.o"],
        cwd=work,
        capture_output=True,
        text=True,
    )
    if r.returncode != 0:
        return None
    (work / "raw.cfg").write_text(CFG)
    r = subprocess.run(
        ["ld65", "-C", "raw.cfg", "p.o", "-o", "ref.bin"],
        cwd=work,
        capture_output=True,
        text=True,
    )
    return (work / "ref.bin").read_bytes() if r.returncode == 0 else None


def z80asm(work, mode):
    env = dict(
        os.environ,
        PATH="/opt/z88dk-2.4/bin:" + os.environ["PATH"],
        ZCCCFG="/opt/z88dk-2.4/lib/config",
    )
    r = subprocess.run(
        ["z80asm", f"-m{mode}", "-b", "main.s"],
        cwd=work,
        capture_output=True,
        text=True,
        env=env,
    )
    out = work / "main.bin"
    return out.read_bytes() if r.returncode == 0 and out.exists() else None


def main() -> None:
    for name, source, ref in (
        ("6502", CA65, lambda w: ca65(w, "6502")),
        ("6502x", CA65X, lambda w: ca65(w, "6502X")),
        ("65c02", CA65C02, lambda w: ca65(w, "65C02")),
        ("z80", Z80, lambda w: z80asm(w, "z80_strict")),
        ("z80u", Z80U, lambda w: z80asm(w, "z80")),
        ("8080", I8080, lambda w: z80asm(w, "8080")),
    ):
        work = OUT / name
        work.mkdir(parents=True, exist_ok=True)
        (work / "main.s").write_text(source)
        (work / "data.bin").write_bytes(DATA)
        binary = ref(work)
        if binary is None:
            raise SystemExit(f"referencja odrzuca Locals/{name}/main.s")
        (work / "expected.bin").write_bytes(binary)
        for f in work.glob("*"):
            if f.is_file() and f.name not in ("main.s", "data.bin", "expected.bin"):
                f.unlink()
        print(f"{name}: expected.bin ({len(binary)} B)")


if __name__ == "__main__":
    main()
