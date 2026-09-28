; Warunkowe dla 8080 (z80asm -m8080): IF/ELSE/ENDIF bez ELIF, jak Intel ASM80.
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
