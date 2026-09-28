; Program porównywany z ca65: stałe, etykiety, odwołania w przód, zero page kontra absolute,
; skoki względne w obie strony, wyrażenia, operatory < >, dyrektywy danych.
ptr     = $10               ; stała zero page zdefiniowana przed użyciem
screen  = $0400
.include "part0.inc"
        dex
        bpl start
finish: rts
message: .byte "HELLO", 0
table:  .word start, finish, table
        .byte <table, >table, count*2+1, %1010, 'A'
        .res 3, $EA
later   = $20
