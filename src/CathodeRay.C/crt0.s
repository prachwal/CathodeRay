; crt0.s — start runtime mini-C. Linkowany ZAWSZE pierwszy (przed wyjściem
; codegenu): inicjalizuje SP, zeruje BSS od __bss_start do __bss_end, woła procedury z tablicy INIT
; (inicjalizatory globali niestałe), woła main, HLT.
; __bss_start definiuje crt0 (1-bajtowy chunk BSS): linker układa chunki BSS
; segment-major (crt0, moduły); __bss_end to koniec ostatniego chunku (linker
; dodaje symbol __<segment>_end; bez linkera daje go codegen na końcu BSS).
; Rozmiar BSS ogranicza tylko obszar w .cfg. main importuje z modułów.
; Umówione komórki konwencji (współdzielone między modułami, jak __bss_start):
; cc_arg1 (wejście), cc_arg2..cc_arg6 (kolejne argumenty), cc_ret (powrót).
.global __bss_start
.extern main
.extern __bss_end
.extern __init_start
.extern __init_end
.global cc_arg1
.global cc_arg1_h
.global cc_arg2
.global cc_arg2_h
.global cc_arg3
.global cc_arg3_h
.global cc_arg4
.global cc_arg4_h
.global cc_arg5
.global cc_arg5_h
.global cc_arg6
.global cc_arg6_h
.global cc_ret
.global cc_ret_h
.segment "CODE"
LDSP 255
LDA cc_bs
STA cc_bp
LDA cc_bs+1
STA cc_bp+1
__bss_next: LDA cc_bp
STA __bss_site+1
LDA cc_bp+1
STA __bss_site+2
LDI 0
__bss_site: STA 0
LDA cc_bp
INC
STA cc_bp
BNE __bss_cmp
LDA cc_bp+1
INC
STA cc_bp+1
__bss_cmp: LDA cc_be
STA __bss_cmp1+1
LDA cc_bp
__bss_cmp1: SUB 0
BNE __bss_next
LDA cc_be+1
STA __bss_cmp2+1
LDA cc_bp+1
__bss_cmp2: SUB 0
BNE __bss_next
LDA cc_is
STA cc_ip
LDA cc_is+1
STA cc_ip+1
__init_next: LDA cc_ie
STA __init_c1+1
LDA cc_ip
__init_c1: SUB 0
BNE __init_go
LDA cc_ie+1
STA __init_c2+1
LDA cc_ip+1
__init_c2: SUB 0
BEQ __init_done
__init_go: LDA cc_ip
STA __init_l1+1
LDA cc_ip+1
STA __init_l1+2
__init_l1: LDA 0
STA __init_call+1
LDA cc_ip
ADD 1
STA cc_it
LDA cc_ip+1
ADC 0
STA cc_it+1
LDA cc_it
STA __init_l2+1
LDA cc_it+1
STA __init_l2+2
__init_l2: LDA 0
STA __init_call+2
__init_call: CALL 0
LDA cc_ip
ADD 2
STA cc_ip
LDA cc_ip+1
ADC 0
STA cc_ip+1
JMP __init_next
__init_done: CALL main
HLT

.global cc_retbuf
.segment "BSS"
__bss_start: .res 1
.segment "DATA"
cc_retbuf: .res 64
cc_arg1: .byte 0
cc_arg1_h: .byte 0
cc_arg2: .byte 0
cc_arg2_h: .byte 0
cc_arg3: .byte 0
cc_arg3_h: .byte 0
cc_arg4: .byte 0
cc_arg4_h: .byte 0
cc_arg5: .byte 0
cc_arg5_h: .byte 0
cc_arg6: .byte 0
cc_arg6_h: .byte 0
cc_ret: .byte 0
cc_ret_h: .byte 0
cc_bs: .word __bss_start
cc_be: .word __bss_end
cc_bp: .word 0
cc_is: .word __init_start
cc_ie: .word __init_end
cc_ip: .word 0
cc_it: .word 0
