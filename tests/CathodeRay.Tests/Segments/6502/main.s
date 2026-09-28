.segment "CODE"
start: lda #1
jmp done
.segment "DATA"
val: .byte 9
table: .word start
.bss
buf: .byte 0
.segment "CODE"
done: lda val
rts
