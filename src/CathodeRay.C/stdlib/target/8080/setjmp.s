; setjmp.s (8080) — setjmp(env) / longjmp(env, val). env: SP po powrocie (2 B) + adres powrotu (2 B); wynik w HL (ABI v1).
GLOBAL setjmp
GLOBAL longjmp
EXTERN cc_arg1
EXTERN cc_arg2
SEGMENT "CODE"
setjmp:
pop d
lhld cc_arg1
mov b,h
mov c,l
lxi h,0
dad sp
mov a,l
stax b
inx b
mov a,h
stax b
inx b
mov a,e
stax b
inx b
mov a,d
stax b
push d
lxi h,0
ret
longjmp:
lhld cc_arg1
mov e,m
inx h
mov d,m
inx h
mov c,m
inx h
mov b,m
lhld cc_arg2
mov a,h
ora l
jnz __lj_val
inx h
__lj_val:
xchg
sphl
push b
xchg
ret
