; rt_div8.s — dzielenie 8-bit bez znaku dla stuba (A / X: iloraz w A, reszta w X). Linkowane na żądanie przez cc.
.segment "CODE"
.global cc_divmod
.proc cc_divmod
CPX 0
BEQ cc_d_zero
STA cc_d_n
TXA
STA cc_d_p1+1
STA cc_d_p2+1
LDX 0
LDI 0
STA cc_d_r
LDI 8
STA cc_d_c
cc_d_loop: LDA cc_d_n
SHL
STA cc_d_n
LDA cc_d_r
ADC cc_d_r,X
STA cc_d_r
BCS cc_d_force
cc_d_p1: SUB 0
BCC cc_d_next
STA cc_d_r
JMP cc_d_inc
cc_d_force: LDA cc_d_r
cc_d_p2: SUB 0
STA cc_d_r
cc_d_inc: LDA cc_d_n
INC
STA cc_d_n
cc_d_next: LDA cc_d_c
SUB 1
STA cc_d_c
BNE cc_d_loop
LDA cc_d_r
TAX
LDA cc_d_n
RET
cc_d_zero: LDI 0
TAX
RET
.endproc
.segment "BSS"
cc_d_n: .res 1
cc_d_r: .res 1
cc_d_c: .res 1
