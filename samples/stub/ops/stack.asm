; Stos i podprogramy: LDSP, PUSH/POP (LIFO), zagniezdzone CALL/RET.
; Pokrywa: LDI, LDSP, PUSH, POP, STA, CALL, ADD, RET, HLT.
LDSP 240        ; stos na strone 01F0h (program jest maly)
LDI 1
PUSH
LDI 2
PUSH
POP            ; A=2 (LIFO)
STA r1
POP            ; A=1
STA r0
LDSP 255       ; stos z powrotem na reset
LDI 0
CALL level1    ; 0+1+10 = 11
STA r_call
HLT
level1: ADD 1
CALL level2
RET
level2: ADD 10
RET
r1: .byte 0
r0: .byte 0
r_call: .byte 0
