.segment "CODE"
lax $12
.segment "DATA"
val: .byte 9
.bss
buf: .byte 0
.segment "CODE"
rts
