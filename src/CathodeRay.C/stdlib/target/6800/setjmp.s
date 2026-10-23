; setjmp.s (6800) — setjmp(env) / longjmp(env, val). env: wartość TSX (SP+1, 2 B) + adres powrotu z JSR (2 B), starszy bajt pierwszy;
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
ldaa z:cc_arg1
staa __sj_p+1
ldaa z:cc_arg1_h
staa __sj_p
tsx
stx __sj_x
ldaa 0,x
staa __sj_h
ldaa 1,x
staa __sj_l
ldx __sj_p
ldaa __sj_x
staa 0,x
ldaa __sj_x+1
staa 1,x
ldaa __sj_h
staa 2,x
ldaa __sj_l
staa 3,x
clra
staa z:cc_ret
staa z:cc_ret_h
rts
longjmp:
ldaa z:cc_arg1
staa __sj_p+1
ldaa z:cc_arg1_h
staa __sj_p
ldx __sj_p
ldaa 0,x
staa __sj_x
ldaa 1,x
staa __sj_x+1
ldaa 2,x
staa __sj_h
ldaa 3,x
staa __sj_l
ldaa z:cc_arg2
oraa z:cc_arg2_h
bne __lj_val
ldaa #1
staa z:cc_arg2
__lj_val:
ldaa z:cc_arg2
staa z:cc_ret
ldaa z:cc_arg2_h
staa z:cc_ret_h
ldx __sj_x
txs
ldaa __sj_h
staa 0,x
ldaa __sj_l
staa 1,x
rts
.segment "BSS"
__sj_p: .res 2
__sj_x: .res 2
__sj_h: .res 1
__sj_l: .res 1
