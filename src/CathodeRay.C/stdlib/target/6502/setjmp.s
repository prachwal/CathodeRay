; setjmp.s (6502) — setjmp(env) / longjmp(env, val). env: S po powrocie (1 B, 1 B wolny) + adres powrotu z JSR (2 B); wynik w cc_ret/cc_ret_h.
.global setjmp
.global longjmp
.extern cc_arg1
.extern cc_arg2
.extern cc_arg2_h
.extern cc_ret
.extern cc_ret_h
.segment "CODE"
setjmp:
tsx
ldy #0
txa
clc
adc #2
sta (cc_arg1),y
iny
lda $0101,x
sta (cc_arg1),y
iny
lda $0102,x
sta (cc_arg1),y
lda #0
sta z:cc_ret
sta z:cc_ret_h
rts
longjmp:
ldy #0
lda (cc_arg1),y
sec
sbc #2
tax
iny
lda (cc_arg1),y
sta $0101,x
iny
lda (cc_arg1),y
sta $0102,x
txs
lda z:cc_arg2
ora z:cc_arg2_h
bne __lj_val
lda #1
sta z:cc_arg2
__lj_val:
lda z:cc_arg2
sta z:cc_ret
lda z:cc_arg2_h
sta z:cc_ret_h
rts
