EXTERN getval
GLOBAL main
SEGMENT "CODE"
main:   LDA getval
        STA result
        HLT
SEGMENT "DATA"
result: DS 1
