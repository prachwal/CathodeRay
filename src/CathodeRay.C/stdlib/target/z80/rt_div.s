; rt_div.s (Z80) — __cc_divu: HL = cc_arg1 / cc_arg2 (dzielnik 0 daje 0); __cc_modu: HL = reszta (wynik W<=2 w HL, plan 35).
GLOBAL __cc_divu
GLOBAL __cc_modu
EXTERN cc_arg1
EXTERN cc_arg2
SEGMENT "CODE"
__cc_divu:
ld bc,(cc_arg2)
ld a,b
or c
jr nz,__cc_div_go
ld hl,0
ld (__cc_rem_),hl
ret
__cc_div_go:
ld hl,(cc_arg1)
ex de,hl
ld hl,0
ld a,16
__cc_div_loop:
sla e
rl d
adc hl,hl
jr c,__cc_div_forced
or a
sbc hl,bc
jr nc,__cc_div_quot
add hl,bc
jr __cc_div_next
__cc_div_forced:
or a
sbc hl,bc
__cc_div_quot:
inc e
__cc_div_next:
dec a
jr nz,__cc_div_loop
ld (__cc_rem_),hl
ex de,hl
ret
__cc_modu:
call __cc_divu
ld hl,(__cc_rem_)
ret
SEGMENT "BSS"
__cc_rem_: DS 2
