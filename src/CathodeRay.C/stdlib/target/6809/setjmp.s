; setjmp.s (6809) — setjmp(env) / longjmp(env, val). env: S po powrocie (2 B) + adres powrotu z JSR (2 B), starszy bajt pierwszy;
; wynik w cc_ret/cc_ret_h.
.global setjmp
.global longjmp
.extern cc_arg1
.extern cc_arg1_h
.extern cc_arg2
.extern cc_arg2_h
.extern cc_ret
.extern cc_ret_h
.segment "CODE"
setjmp:
lda z:cc_arg1
sta __sj_p+1
lda z:cc_arg1_h
sta __sj_p
ldx __sj_p
leay 2,s
sty 0,x
ldd 0,s
std 2,x
clr z:cc_ret
clr z:cc_ret_h
rts
longjmp:
lda z:cc_arg1
sta __sj_p+1
lda z:cc_arg1_h
sta __sj_p
ldx __sj_p
ldd 2,x
std __sj_r
lds 0,x
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
ldx __sj_r
jmp 0,x
.segment "BSS"
__sj_p: .res 2
__sj_r: .res 2
