; ALU immediate: ADD, SUB, ADC + CLC/SEC; carry obserwowane przez BCS.
; Pokrywa: LDI, ADD, STA, BCS, JMP, SUB, CLC, SEC, ADC, HLT.
LDI 100
ADD 27        ; 127
STA r_add
LDI 200
ADD 100       ; 44, C=1
STA r_wrap
BCS carry_ok
LDI 0
JMP carry_done
carry_ok: LDI 1
carry_done: STA r_carry
LDI 50
SUB 20        ; 30, C=1 (bez pozyczki)
STA r_sub
LDI 10
SUB 20        ; 246, C=0 (pozyczka)
STA r_borrow
CLC
LDI 5
ADC 10        ; 5+10+0 = 15
STA r_adc0
SEC
LDI 5
ADC 10        ; 5+10+1 = 16
STA r_adc1
HLT
r_add: .byte 0
r_wrap: .byte 0
r_carry: .byte 0
r_sub: .byte 0
r_borrow: .byte 0
r_adc0: .byte 0
r_adc1: .byte 0
