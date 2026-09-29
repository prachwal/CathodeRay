; rt_mul.s (Z80) — __cc_mul: cc_ret = cc_arg1 * cc_arg2 (16 bitów). cc_argN i cc_argN_h leżą w pamięci obok siebie (crt0).
GLOBAL __cc_mul
EXTERN cc_arg1
EXTERN cc_arg2
EXTERN cc_ret
SEGMENT "CODE"
__cc_mul:
ld hl,(cc_arg1)
ex de,hl
ld bc,(cc_arg2)
ld hl,0
ld a,16
__cc_mul_loop:
add hl,hl
sla c
rl b
jr nc,__cc_mul_skip
add hl,de
__cc_mul_skip:
dec a
jr nz,__cc_mul_loop
ld (cc_ret),hl
ret
