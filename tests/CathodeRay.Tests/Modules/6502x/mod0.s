.extern getval
.global main
.segment "CODE"
main:   lda getval
        sta result
        rts
.segment "DATA"
result: .byte 0
