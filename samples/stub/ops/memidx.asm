; Ladowanie/skladowanie + tryby indeksowane (,X) z X=0 i X=4.
; Pokrywa: LDI, STA, LDA, LDX, LDA (,X), STA (,X), ADD (,X), ADC (,X), CLC, SEC, HLT.
LDI 11
STA a
LDA a
STA b          ; b=11
LDX 4
LDA tab,X      ; tab[4]=50
STA r_idx
LDA tab,X
ADD add1,X     ; 50+5=55
STA r_addx
CLC
LDA tab,X
ADC add1,X     ; 50+5+0=55
STA r_adcx
SEC
LDA tab,X
ADC add1,X     ; 50+5+1=56
STA r_adcx2
LDI 77
STA buf,X      ; buf[4]=77
HLT
a: .byte 0
b: .byte 0
r_idx: .byte 0
r_addx: .byte 0
r_adcx: .byte 0
r_adcx2: .byte 0
tab: .byte 10
tab1: .byte 20
tab2: .byte 30
tab3: .byte 40
tab4: .byte 50
add1: .byte 1
add1b: .byte 2
add1c: .byte 3
add1d: .byte 4
add1e: .byte 5
buf: .byte 0
buf1: .byte 0
buf2: .byte 0
buf3: .byte 0
buf4: .byte 0
