; Logika bitowa immediate + indeksowana (+ LDA indeksowane).
; Pokrywa: LDI, AND, ORA, EOR, NOT, STA, LDX, LDA (,X), HLT.
LDI 240
AND 60         ; 0xF0 & 0x3C = 0x30 = 48
STA r_and
LDI 240
ORA 15         ; 0xF0 | 0x0F = 0xFF = 255
STA r_ora
LDI 255
EOR 15         ; 0xFF ^ 0x0F = 0xF0 = 240
STA r_eor
LDI 60
NOT            ; ~0x3C = 0xC3 = 195
STA r_not
LDX 0
LDA tab,X      ; 7
STA r_ldx
LDA tab,X
AND m1,X       ; 7 & 5 = 5
STA r_andx
LDA tab,X
ORA m1,X       ; 7 | 5 = 7
STA r_orax
LDA tab,X
EOR m1,X       ; 7 ^ 5 = 2
STA r_eorx
HLT
r_and: .byte 0
r_ora: .byte 0
r_eor: .byte 0
r_not: .byte 0
r_ldx: .byte 0
r_andx: .byte 0
r_orax: .byte 0
r_eorx: .byte 0
tab: .byte 7
m1: .byte 5
