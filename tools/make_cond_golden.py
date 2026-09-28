#!/usr/bin/env python3
"""Generuje wzorce asemblacji warunkowej w tests/CathodeRay.Tests/Cond/.

Dla 6502 (ca65) i z80 (z80asm) pisze main.s z .if/.elseif/.else/.endif
(wyражения z porównaniami, zagnieżdżenia, symbole tylko z aktywnej gałęzi),
składa referencją i zapisuje expected.bin. Weryfikuje CondTests.cs.
"""

import os
import pathlib
import subprocess
import sys

REPO = pathlib.Path(__file__).resolve().parent.parent
OUT = REPO / "tests" / "CathodeRay.Tests" / "Cond"
CFG = "MEMORY { M: start=$0000, size=$10000, file=%O; }\nSEGMENTS { CODE: load=M, type=rw; }\n"

CA65 = """; Warunkowe porównywane z ca65: wybór wariantu, elseif, zagnieżdżenie,
; symbole tylko z aktywnej gałęzi, porównania w wyrażeniach.
debug   = 1
platform = 2

        .org $0600
start:  .if debug = 1
        ldx #1
        .elseif platform = 2
        ldx #2
        .else
        ldx #3
        .endif
        .if debug <> 0
        lda #<active
        .endif
        .if platform > 1
        .if debug <> 0
        lda #>active
        .endif
        .endif
        .if platform <= 1
        lda #$FF
        .else
        sta active
        .endif
        rts
active: .byte $42
"""

Z80 = """; Warunkowe porównywane z z80asm: wybór wariantu, ELIF, zagnieżdżenie,
; symbole tylko z aktywnej gałęzi, porównania w wyrażeniach.
DEBUG   EQU 1
PLATFORM EQU 2

        ORG 8000H
start:  IF DEBUG == 1
        LD B,1
        ELIF PLATFORM == 2
        LD B,2
        ELSE
        LD B,3
        ENDIF
        IF DEBUG != 0
        LD A,(active)
        ENDIF
        IF PLATFORM > 1
        IF DEBUG <> 0
        LD A,(active+1)
        ENDIF
        ENDIF
        IF PLATFORM <= 1
        LD A,0FFH
        ELSE
        LD (active),A
        ENDIF
        RET
active: DEFW 1234H
"""


def ca65(work):
    r = subprocess.run(
        ["ca65", "--cpu", "6502", "main.s", "-o", "p.o"],
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


CA65X = """; Warunkowe dla 6502X (ca65): nieudokumentowany LAX tylko w aktywnej gałęzi.
debug   = 0

        .org $0600
        .if debug = 1
        lax $12
        .else
        lda $12
        .endif
        .if debug <> 0
        nop
        .endif
        rts
"""

CA65C02 = """; Warunkowe dla 65C02 (ca65): STZ/BRA tylko w aktywnej gałęzi.
debug   = 1
ptr     = $20

        .org $0800
        .if debug = 1
        stz ptr
        .else
        lda ptr
        .endif
        .if debug = 0
        nop
        .elseif debug = 1
        bra done
        lda #$FF
done:   rts
        .endif
"""

Z80U = """; Warunkowe dla z80u (z80asm -mz80): nieudokumentowane IXH tylko w aktywnej gałęzi.
DEBUG   EQU 0

        ORG 8000H
        IF DEBUG == 1
        LD A,IXH
        ELSE
        LD A,IXL
        ENDIF
        IF DEBUG != 0
        NOP
        ENDIF
        RET
"""

I8080 = """; Warunkowe dla 8080 (z80asm -m8080): IF/ELSE/ENDIF bez ELIF, jak Intel ASM80.
VAL     EQU 1

        ORG 100H
        IF VAL = 1
        MVI A,1
        ELSE
        MVI A,2
        ENDIF
        IF VAL = 0
        MVI B,1
        ENDIF
        HLT
"""


def ca65x(work):
    r = subprocess.run(
        ["ca65", "--cpu", "6502X", "main.s", "-o", "p.o"],
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


def ca65c02(work):
    r = subprocess.run(
        ["ca65", "--cpu", "65C02", "main.s", "-o", "p.o"],
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


def main() -> None:
    for name, source, ref in (
        ("6502", CA65, lambda w: ca65(w)),
        ("z80", Z80, lambda w: z80asm(w, "z80_strict")),
        ("6502x", CA65X, lambda w: ca65x(w)),
        ("65c02", CA65C02, lambda w: ca65c02(w)),
        ("z80u", Z80U, lambda w: z80asm(w, "z80")),
        ("8080", I8080, lambda w: z80asm(w, "8080")),
    ):
        work = OUT / name
        work.mkdir(parents=True, exist_ok=True)
        (work / "main.s").write_text(source)
        binary = ref(work)
        if binary is None:
            raise SystemExit(f"referencja odrzuca Cond/{name}/main.s")
        (work / "expected.bin").write_bytes(binary)
        for f in work.glob("*"):
            if f.is_file() and f.name not in ("main.s", "expected.bin"):
                f.unlink()
        print(f"{name}: expected.bin ({len(binary)} B)")


if __name__ == "__main__":
    main()
