; rt_mul8.s — mnożenie 8-bit dla stuba (A * X -> A), shift-add. Linkowane na żądanie przez cc.
.segment "CODE"
.global cc_mul8
.proc cc_mul8
STA cc_m_a
TXA
STA cc_m_b
LDX 0
LDI 0
STA cc_m_acc
cc_m_loop: LDA cc_m_b
BEQ cc_m_done
SHR
STA cc_m_b
BCC cc_m_skip
LDA cc_m_acc
ADD cc_m_a,X
STA cc_m_acc
cc_m_skip: LDA cc_m_a
ADD cc_m_a,X
STA cc_m_a
JMP cc_m_loop
cc_m_done: LDA cc_m_acc
RET
.endproc
.segment "BSS"
cc_m_acc: .res 1
cc_m_a: .res 1
cc_m_b: .res 1
