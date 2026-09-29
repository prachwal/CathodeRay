; rt_div.s (6502) — __cc_divu: cc_ret = cc_arg1 / cc_arg2, reszta w __cc_rem_(zawsze 0 dla dzielnika 0); __cc_modu: cc_ret = reszta.
.global __cc_divu
.global __cc_modu
.extern cc_arg1
.extern cc_arg1_h
.extern cc_arg2
.extern cc_arg2_h
.extern cc_ret
.extern cc_ret_h
.segment "CODE"
__cc_divu:
lda z:cc_arg2
ora z:cc_arg2_h
bne __cc_div_go
lda #0
sta z:cc_ret
sta z:cc_ret_h
sta __cc_rem_
sta __cc_rem_+1
rts
__cc_div_go:
lda #0
sta __cc_rem_
sta __cc_rem_+1
ldx #16
__cc_div_loop:
asl z:cc_arg1
rol z:cc_arg1_h
rol __cc_rem_
rol __cc_rem_+1
bcs __cc_div_sub
lda __cc_rem_
cmp z:cc_arg2
lda __cc_rem_+1
sbc z:cc_arg2_h
bcc __cc_div_next
__cc_div_sub:
lda __cc_rem_
sec
sbc z:cc_arg2
sta __cc_rem_
lda __cc_rem_+1
sbc z:cc_arg2_h
sta __cc_rem_+1
inc z:cc_arg1
__cc_div_next:
dex
bne __cc_div_loop
lda z:cc_arg1
sta z:cc_ret
lda z:cc_arg1_h
sta z:cc_ret_h
rts
__cc_modu:
jsr __cc_divu
lda __cc_rem_
sta z:cc_ret
lda __cc_rem_+1
sta z:cc_ret_h
rts
.segment "BSS"
__cc_rem_: .res 2
