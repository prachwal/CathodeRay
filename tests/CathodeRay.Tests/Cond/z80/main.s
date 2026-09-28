; Warunkowe porównywane z z80asm: wybór wariantu, ELIF, zagnieżdżenie,
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
