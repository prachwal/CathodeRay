EXTERN getval
GLOBAL main
SEGMENT "CODE"
main:   LD A,(getval)
        LD (result),A
        RET
SEGMENT "DATA"
result: DEFS 1
