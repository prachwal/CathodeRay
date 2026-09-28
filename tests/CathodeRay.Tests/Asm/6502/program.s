; Program porównywany z ca65: stałe, etykiety, odwołania w przód, zero page kontra absolute,
; skoki względne w obie strony, wyrażenia, operatory < >, dyrektywy danych.
ptr     = $10               ; stała zero page zdefiniowana przed użyciem
screen  = $0400
count   = 8

        .org $0600
start:  ldx #count
        lda #<message
        sta ptr
        lda #>message
        sta ptr+1
        ldy #0
loop:   lda (ptr),y
        beq done
        sta screen,y
        iny
        bne loop
done:   jmp finish          ; odwołanie w przód
        lda later           ; stała zdefiniowana później: forma absolute, jak w ca65
        dex
        bpl start
finish: rts
message: .byte "HELLO", 0
table:  .word start, finish, table
        .byte <table, >table, count*2+1, %1010, 'A'
        .res 3, $EA
later   = $20
