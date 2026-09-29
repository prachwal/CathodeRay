; rt_mul.s (6502) — __cc_mul: cc_ret = cc_arg1 * cc_arg2 (16 bitów, młodsze bity wyniku). Niszczy cc_arg1 i cc_arg2.
.global __cc_mul
.extern cc_arg1
.extern cc_arg1_h
.extern cc_arg2
.extern cc_arg2_h
.extern cc_ret
.extern cc_ret_h
.segment "CODE"
__cc_mul:
lda #0
sta z:cc_ret
sta z:cc_ret_h
__cc_mul_loop:
lda z:cc_arg2
ora z:cc_arg2_h
beq __cc_mul_done
lsr z:cc_arg2_h
ror z:cc_arg2
bcc __cc_mul_skip
clc
lda z:cc_ret
adc z:cc_arg1
sta z:cc_ret
lda z:cc_ret_h
adc z:cc_arg1_h
sta z:cc_ret_h
__cc_mul_skip:
asl z:cc_arg1
rol z:cc_arg1_h
jmp __cc_mul_loop
__cc_mul_done:
rts
