; Transfery miedzyrejestrowe + INC/DEC/INX/DEX + NOP/JMP.
; Pokrywa: LDI, TAX, STA, TXA, INC, DEC, INX, DEX, NOP, JMP, HLT.
LDI 42
TAX            ; X=42, A zostaje 42
STA r0
TXA
INC            ; 43
STA r1
DEC            ; 42
STA r2
INX            ; X=43
DEX            ; X=42
TXA
STA r_x
NOP
JMP skip
LDI 0          ; pomijane
skip: LDI 7
STA r3
HLT
r0: .byte 0
r1: .byte 0
r2: .byte 0
r_x: .byte 0
r3: .byte 0
