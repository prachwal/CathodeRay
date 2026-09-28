#!/usr/bin/env python3
"""Generuje wzorce zakresów leksykalnych w tests/CathodeRay.Tests/Scopes/.

Dla 6502/6502x/65c02 pisze main.s (.proc/.scope, kolizje loop, dostęp ::,
zagnieżdżenia), składa referencją ca65+ld65 i zapisuje expected.bin.
Weryfikuje ScopeTests.cs (odkrywanie po katalogu).
"""

import pathlib
import subprocess
import sys

REPO = pathlib.Path(__file__).resolve().parent.parent
OUT = REPO / "tests" / "CathodeRay.Tests" / "Scopes"
CFG = "MEMORY { M: start=$0000, size=$10000, file=%O; }\nSEGMENTS { CODE: load=M, type=rw; }\n"

CA65 = """; Zakresy porównywane z ca65: .proc z kolizją loop, dostęp foo::loop,
; .scope bez etykiety, zagnieżdżenie outer::inner::val, anonimowy .scope.
        .org $0600
.proc foo
loop:   lda #1
        bne loop
        rts
.endproc
.proc bar
loop:   lda #2
        bne loop
        rts
.endproc
.scope glob
shared: nop
.endscope
.scope outer
.scope inner
val:    nop
.endscope
.endscope
.scope
hidden: nop
.endscope
        lda foo::loop
        lda bar::loop
        lda glob::shared
        lda outer::inner::val
        rts
"""

CA65X = """; Zakresy dla 6502X (ca65): LAX w procedurach z kolizją loop.
        .org $0600
.proc foo
loop:   lax $12
        bpl loop
        rts
.endproc
.proc bar
loop:   lax $13
        bpl loop
        rts
.endproc
        lda foo::loop
        lda bar::loop
        rts
"""

CA65C02 = """; Zakresy dla 65C02 (ca65): STZ/BRA w procedurach z kolizją loop.
        .org $0800
.proc foo
loop:   stz $20
        bra loop
.endproc
.proc bar
loop:   stz $21
        bra loop
.endproc
        lda foo::loop
        lda bar::loop
        rts
"""


def ca65(work, cpu):
    r = subprocess.run(
        ["ca65", "--cpu", cpu, "main.s", "-o", "p.o"],
        cwd=work,
        capture_output=True,
        text=True,
    )
    if r.returncode != 0:
        print(r.stderr[-1000:])
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
    for name, source, cpu in (
        ("6502", CA65, "6502"),
        ("6502x", CA65X, "6502X"),
        ("65c02", CA65C02, "65C02"),
    ):
        work = OUT / name
        work.mkdir(parents=True, exist_ok=True)
        (work / "main.s").write_text(source)
        binary = ca65(work, cpu)
        if binary is None:
            raise SystemExit(f"referencja odrzuca Scopes/{name}/main.s")
        (work / "expected.bin").write_bytes(binary)
        for f in work.glob("*"):
            if f.is_file() and f.name not in ("main.s", "expected.bin"):
                f.unlink()
        print(f"{name}: expected.bin ({len(binary)} B)")


if __name__ == "__main__":
    main()
