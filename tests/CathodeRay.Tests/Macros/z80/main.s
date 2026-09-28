; z80asm: MACRO/ENDM, parametry, LOCAL, makro wołające makro.
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
