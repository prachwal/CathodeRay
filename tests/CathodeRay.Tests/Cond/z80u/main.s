; Warunkowe dla z80u (z80asm -mz80): nieudokumentowane IXH tylko w aktywnej gałęzi.
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
