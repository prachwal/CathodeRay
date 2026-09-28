; Program porównywany z z80asm (-mz80_strict): stałe EQU, etykiety, DJNZ, JR warunkowe, (IX+d) i (IX),
; DDCB, stałe BIT/IM/RST, dane DEFM/DEFB/DEFW/DEFS, $ jako bieżący adres.
count   EQU 8
port    EQU 0FEH

INCLUDE "part0.inc"
INCLUDE "part1.inc"
        RST 38H
        LD HL,(table)
        LD A,(table+1)
        LD A,(2+3)*4
        JR $
message: DEFM "HI"
        DEFB 0, count*2, 'Z'
table:  DEFW start, done, $
        DEFS 3
