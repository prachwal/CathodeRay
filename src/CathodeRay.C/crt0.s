; crt0.s — start runtime mini-C. Linkowany ZAWSZE pierwszy (przed wyjściem
; codegenu): inicjalizuje SP, zeruje 256 B od __bss_start, woła main, HLT.
; __bss_start definiuje crt0 (1-bajtowy chunk BSS): linker układa chunki BSS
; segment-major (crt0, moduły), więc pętla czyści wszystkie (limit: obszar BSS
; w .cfg ≤ 256 B, linker pilnuje przepełnienia). main importuje z modułów.
; Umówione komórki konwencji (współdzielone między modułami, jak __bss_start):
; cc_arg1 (wejście), cc_arg2 (2. int-arg), cc_ret (powrót).
.global __bss_start
.extern main
.global cc_arg1
.global cc_arg1_h
.global cc_arg2
.global cc_arg2_h
.global cc_ret
.global cc_ret_h
.segment "CODE"
LDSP 255
LDX 0
LDI 0
__bss_zero: STA __bss_start,X
INX
BNE __bss_zero
CALL main
HLT
.segment "BSS"
__bss_start: .res 1
.segment "DATA"
cc_arg1: .byte 0
cc_arg1_h: .byte 0
cc_arg2: .byte 0
cc_arg2_h: .byte 0
cc_ret: .byte 0
cc_ret_h: .byte 0
