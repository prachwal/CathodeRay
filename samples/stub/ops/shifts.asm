; Przesuniecia + lancuch carry (SHL/SHR, potem ADC z C); carry przez BCS/BCC.
; Pokrywa: LDI, SHL, SHR, STA, BCS, BCC, JMP, SEC, CLC, ADC, HLT.
LDI 129
SHL            ; 2, C=1
STA r_shl
BCS shl_carry
LDI 0
JMP shl_done
shl_carry: LDI 1
shl_done: STA r_c1
LDI 5
SHL            ; 10, C=0
STA r_shl0
BCC shl_nocarry
LDI 0
JMP shl_done0
shl_nocarry: LDI 1
shl_done0: STA r_c0
LDI 129
SHR            ; 64, C=1
STA r_shr
LDI 5
SHR            ; 2, C=0
STA r_shr0
SEC
LDI 0
ADC 0          ; 0+0+1 = 1 (carry wchodzi)
STA r_adc1
CLC
LDI 0
ADC 0          ; 0+0+0 = 0
STA r_adc0
HLT
r_shl: .byte 0
r_c1: .byte 0
r_shl0: .byte 0
r_c0: .byte 0
r_shr: .byte 0
r_shr0: .byte 0
r_adc1: .byte 0
r_adc0: .byte 0
