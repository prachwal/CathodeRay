#!/usr/bin/env python3
"""Generuje wzorce makr w tests/CathodeRay.Tests/Macros/.

Dla każdego CPU z referencją pisze main.s (definicje z parametrami, wywołania,
.local/LOCAL, makro wołające makro), składa referencją (ca65/ld65, z80asm)
i zapisuje expected.bin. Weryfikuje MacroTests.cs (odkrywanie po katalogu).
"""

import os
import pathlib
import subprocess
import sys

REPO = pathlib.Path(__file__).resolve().parent.parent
OUT = REPO / "tests" / "CathodeRay.Tests" / "Macros"
CFG = "MEMORY { M: start=$0000, size=$10000, file=%O; }\nSEGMENTS { CODE: load=M, type=rw; }\n"

CA65 = """; Makra porównane z ca65: parametr, .local, makro wołające makro.
        .org $0600
        .macro add2 val
        lda #val
        clc
        adc #val
        .endmacro
        .macro load1 val
        .local done
        lda #val
        ldx #val
done:
        .endmacro
        .macro twice v
        add2 v
        add2 v
        .endmacro
start:  add2 5
        load1 7
        twice 3
        rts
"""

CA65X = """; 6502X: LAX w makrze z .local.
        .org $0600
        .macro get zp
        .local done
        lax zp
        bpl done
        lda #0
done:
        .endmacro
        get $12
        get $13
        rts
"""

CA65C02 = """; 65C02: STZ/BRA w makrze.
        .org $0800
        .macro zero ptr
        .local done
        stz ptr
        bra done
        lda #$FF
done:
        .endmacro
        zero $20
        rts
"""

Z80 = """; z80asm: MACRO/ENDM, parametry, LOCAL, makro wołające makro.
ADD2:   MACRO val
        LD A,val
        ADD A,val
        ENDM
LOAD2:  MACRO a, b
        LOCAL done
        LD A,a
        LD B,b
done:
        ENDM
TWICE:  MACRO v
        ADD2 v
        ADD2 v
        ENDM
        ORG 8000H
start:  ADD2 5
        LOAD2 1, 2
        TWICE 3
        RET
"""

Z80U = """; z80u: IXH w makrze z LOCAL.
GET:    MACRO zp
        LOCAL done
        LD A,IXH
        OR A
        JR Z,done
        LD A,IXL
done:
        ENDM
        ORG 8000H
        GET 1
        GET 2
        RET
"""

I8080 = """; 8080: MACRO/ENDM/LOCAL w składni Intela.
PUT:    MACRO v
        LOCAL done
        MVI A,v
        ORA A
        JZ done
        MVI B,v
done:
        ENDM
        ORG 100H
        PUT 9
        PUT 0
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
        binary = ref(work)
        if binary is None:
            raise SystemExit(f"referencja odrzuca Macros/{name}/main.s")
        (work / "expected.bin").write_bytes(binary)
        for f in work.glob("*"):
            if f.is_file() and f.name not in ("main.s", "expected.bin"):
                f.unlink()
        print(f"{name}: expected.bin ({len(binary)} B)")


if __name__ == "__main__":
    main()
