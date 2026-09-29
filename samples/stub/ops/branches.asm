; Porownania CPA/CPX + wszystkie skoki warunkowe + petla DEX.
; Pokrywa: LDI, CPA, BEQ, JMP, BNE, BCC, BCS, LDX, CPX, DEX, LDA, STA, INC, HLT.
LDI 5
CPA 5
BEQ is_eq
LDI 0
JMP eq_done
is_eq: LDI 1
eq_done: STA r_eq
LDI 5
CPA 3
BNE is_ne
LDI 0
JMP ne_done
is_ne: LDI 1
ne_done: STA r_ne
LDI 3
CPA 5          ; 3 < 5: pozyczka, C=0
BCC is_lt
LDI 0
JMP lt_done
is_lt: LDI 1
lt_done: STA r_lt
LDI 5
CPA 3          ; 5 >= 3: brak pozyczki, C=1
BCS is_ge
LDI 0
JMP ge_done
is_ge: LDI 1
ge_done: STA r_ge
LDX 7
CPX 7
BEQ x_eq
LDI 0
JMP xeq_done
x_eq: LDI 1
xeq_done: STA r_xeq
LDX 3
CPX 5          ; X < 5: C=0
BCC x_lt
LDI 0
JMP xlt_done
x_lt: LDI 1
xlt_done: STA r_xlt
LDX 5
LDI 0
STA cnt
loop: LDA cnt
INC
STA cnt
DEX
BNE loop       ; 5 iteracji
LDA cnt
STA r_cnt
HLT
r_eq: .byte 0
r_ne: .byte 0
r_lt: .byte 0
r_ge: .byte 0
r_xeq: .byte 0
r_xlt: .byte 0
cnt: .byte 0
r_cnt: .byte 0
