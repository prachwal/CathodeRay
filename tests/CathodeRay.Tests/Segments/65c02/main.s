.segment "CODE"
stz $20
.segment "DATA"
val: .byte 9
.bss
buf: .byte 0
.segment "CODE"
rts
