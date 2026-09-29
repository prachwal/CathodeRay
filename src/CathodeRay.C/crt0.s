; crt0.s — start runtime mini-C. Linkowany ZAWSZE pierwszy (przed wyjściem
; codegenu): inicjalizuje SP, zeruje 256 B od __bss_start (BSS leży ostatnie,
; więc nadmiar idzie w pusty RAM), woła main, po powrocie HLT.
; Wymaga symboli z wyjścia codegenu: main, __bss_start.
.segment "CODE"
LDSP 255
LDX 0
LDI 0
__bss_zero: STA __bss_start,X
INX
BNE __bss_zero
CALL main
HLT
