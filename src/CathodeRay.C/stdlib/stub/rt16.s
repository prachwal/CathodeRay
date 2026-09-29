; rt16.s — mnożenie i dzielenie 16-bit dla stuba: argumenty w cc_w_a/cc_w_b, wynik w A:X, reszta w cc_w_r.
; cc_div16 dzieli bez znaku, cc_sdiv16 ze znakiem (iloraz do zera, reszta ma znak dzielnej). Linkowane na żądanie przez cc.
.segment "CODE"
.global cc_mul16
.global cc_div16
.global cc_sdiv16
.proc cc_mul16
LDX 0
LDI 0
STA cc_w_r
STA cc_w_r_h
LDI 16
STA cc_w_n
cc_m16_loop: LDA cc_w_r
ADD cc_w_r,X
STA cc_w_r
LDA cc_w_r_h
ADC cc_w_r_h,X
STA cc_w_r_h
LDA cc_w_b
ADD cc_w_b,X
STA cc_w_b
LDA cc_w_b_h
ADC cc_w_b_h,X
STA cc_w_b_h
BCC cc_m16_skip
LDA cc_w_r
ADD cc_w_a,X
STA cc_w_r
LDA cc_w_r_h
ADC cc_w_a_h,X
STA cc_w_r_h
cc_m16_skip: LDA cc_w_n
SUB 1
STA cc_w_n
BNE cc_m16_loop
LDA cc_w_r_h
TAX
LDA cc_w_r
RET
.endproc
.proc cc_div16
LDX 0
LDI 0
STA cc_w_r
STA cc_w_r_h
LDA cc_w_b
ORA cc_w_b_h,X
BEQ cc_d16_zero
LDI 16
STA cc_w_n
cc_d16_loop: LDA cc_w_a
ADD cc_w_a,X
STA cc_w_a
LDA cc_w_a_h
ADC cc_w_a_h,X
STA cc_w_a_h
LDA cc_w_r
ADC cc_w_r,X
STA cc_w_r
LDA cc_w_r_h
ADC cc_w_r_h,X
STA cc_w_r_h
BCS cc_d16_sub
LDA cc_w_b+1
STA rt16_6_patch+1
LDA cc_w_r+1
rt16_6_patch: SUB 0
BCC rt16_1_hless
BEQ rt16_2_heq
JMP rt16_5_cdone
rt16_1_hless:
JMP cc_d16_next
rt16_2_heq:
LDA cc_w_b
STA rt16_7_patch+1
LDA cc_w_r
rt16_7_patch: SUB 0
BCC rt16_3_lless
BEQ rt16_4_leq
JMP rt16_5_cdone
rt16_3_lless:
JMP cc_d16_next
rt16_4_leq:
JMP rt16_5_cdone
rt16_5_cdone:
cc_d16_sub: NOP
LDA cc_w_b
STA rt16_8_s16+1
LDA cc_w_r
rt16_8_s16: SUB 0
STA cc_w_r
BCS rt16_11_s16nb
LDA cc_w_b_h
STA rt16_9_s16+1
LDA cc_w_r_h
rt16_9_s16: SUB 0
SUB 1
STA cc_w_r_h
JMP rt16_12_s16e
rt16_11_s16nb:
LDA cc_w_b_h
STA rt16_10_s16+1
LDA cc_w_r_h
rt16_10_s16: SUB 0
STA cc_w_r_h
rt16_12_s16e:
LDA cc_w_a
INC
STA cc_w_a
cc_d16_next: LDA cc_w_n
SUB 1
STA cc_w_n
BNE cc_d16_loop
LDA cc_w_a_h
TAX
LDA cc_w_a
RET
cc_d16_zero: LDI 0
TAX
RET
.endproc
.proc cc_sdiv16
LDX 0
LDA cc_w_a_h
AND 128
STA cc_w_sa
LDA cc_w_b_h
AND 128
STA cc_w_sb
LDA cc_w_sa
BEQ cc_sd_ap
LDA cc_w_a_h
NOT
STA cc_w_a_h
LDA cc_w_a
NOT
INC
STA cc_w_a
BNE rt16_13_neg
LDA cc_w_a_h
INC
STA cc_w_a_h
rt16_13_neg:
cc_sd_ap: LDA cc_w_sb
BEQ cc_sd_bp
LDA cc_w_b_h
NOT
STA cc_w_b_h
LDA cc_w_b
NOT
INC
STA cc_w_b
BNE rt16_14_neg
LDA cc_w_b_h
INC
STA cc_w_b_h
rt16_14_neg:
cc_sd_bp: CALL cc_div16
STA cc_w_q
TXA
STA cc_w_q_h
LDX 0
LDA cc_w_sa
EOR cc_w_sb,X
BEQ cc_sd_qp
LDA cc_w_q_h
NOT
STA cc_w_q_h
LDA cc_w_q
NOT
INC
STA cc_w_q
BNE rt16_15_neg
LDA cc_w_q_h
INC
STA cc_w_q_h
rt16_15_neg:
cc_sd_qp: LDA cc_w_sa
BEQ cc_sd_rp
LDA cc_w_r_h
NOT
STA cc_w_r_h
LDA cc_w_r
NOT
INC
STA cc_w_r
BNE rt16_16_neg
LDA cc_w_r_h
INC
STA cc_w_r_h
rt16_16_neg:
cc_sd_rp: LDA cc_w_q_h
TAX
LDA cc_w_q
RET
.endproc
.segment "BSS"
.global cc_w_a
cc_w_a: .res 1
.global cc_w_a_h
cc_w_a_h: .res 1
.global cc_w_b
cc_w_b: .res 1
.global cc_w_b_h
cc_w_b_h: .res 1
.global cc_w_r
cc_w_r: .res 1
.global cc_w_r_h
cc_w_r_h: .res 1
.global cc_w_n
cc_w_n: .res 1
.global cc_w_sa
cc_w_sa: .res 1
.global cc_w_sb
cc_w_sb: .res 1
.global cc_w_q
cc_w_q: .res 1
.global cc_w_q_h
cc_w_q_h: .res 1
