; Moduł główny dema konsolidacji: woła funkcję z drugiego modułu.
;   cathode asm samples/stub/features/link/main.s --cpu stub -f obj -o /tmp/m0.o
;   cathode asm samples/stub/features/link/lib.s --cpu stub -f obj -o /tmp/m1.o
;   cathode link /tmp/m0.o /tmp/m1.o -m samples/stub/features/link/map.cfg -o /tmp/prog.bin
;   cathode stub run /tmp/prog.bin --dump 0x0C:2
; Wynik: result = 15.

.extern add10
.global main
.global result

.segment "CODE"
main:
LDI 5
CALL add10
STA result
HLT

.segment "DATA"
result: .byte 0
