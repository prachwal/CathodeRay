; Program porównywany z z80asm (-mz80_strict): stałe EQU, etykiety, DJNZ, JR warunkowe, (IX+d) i (IX),
; DDCB, stałe BIT/IM/RST, dane DEFM/DEFB/DEFW/DEFS, $ jako bieżący adres.
count   EQU 8
port    EQU 0FEH

        ORG 8000H
start:  LD IX,table
        LD B,count
        XOR A
loop:   ADD A,(IX+0)
        ADD A,(IX)
        INC IX
        DJNZ loop
        LD (IX-2),A
        RLC (IX+1)
        BIT 3+4,(IX-1)
        SET 0,(IY+127)
        RES 7,(HL)
        OUT (port),A
        IN A,(C)
        JR NZ,done
        JP (IX)
        EX AF,AF'           ; zamiana z rejestrami alternatywnymi
done:   IM 2
        RST 38H
        LD HL,(table)
        LD A,(table+1)
        LD A,(2+3)*4
        JR $
message: DEFM "HI"
        DEFB 0, count*2, 'Z'
table:  DEFW start, done, $
        DEFS 3
