; setjmp.s (Z80) — setjmp(env) / longjmp(env, val). env: SP po powrocie (2 B) + adres powrotu (2 B); wynik w HL (ABI v1).
GLOBAL setjmp
GLOBAL longjmp
EXTERN cc_arg1
EXTERN cc_arg2
SEGMENT "CODE"
setjmp:
pop de
ld bc,(cc_arg1)
ld hl,0
add hl,sp
ld a,l
ld (bc),a
inc bc
ld a,h
ld (bc),a
inc bc
ld a,e
ld (bc),a
inc bc
ld a,d
ld (bc),a
push de
ld hl,0
ret
longjmp:
ld hl,(cc_arg1)
ld e,(hl)
inc hl
ld d,(hl)
inc hl
ld c,(hl)
inc hl
ld b,(hl)
ld hl,(cc_arg2)
ld a,h
or l
jr nz,__lj_val
inc hl
__lj_val:
ex de,hl
ld sp,hl
push bc
ex de,hl
ret
