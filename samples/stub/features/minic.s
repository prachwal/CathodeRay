; Demo konwencji mini-C (docs/stub-calling-conv.md): argument w A, wynik w A,
; zmienne absolutne, 2 poziomy zagnieżdżenia (main → square → mul8),
; porównanie < przez CPA+BCC, pętla for przez X.
; Wynik: s1 = 16, s2 = 25, flag = 0, flag2 = 7, X = 5.

start:  LDI 4
        CALL square
        STA s1
        LDI 5
        CALL square
        STA s2
        LDA s1
        CPA 20
        BCS ge
        LDI 0
        JMP stored
ge:     LDI 1
stored: STA flag
        LDX 0
count:  INX
        CPX 5
        BNE count
        LDA s2
        CPA 20
        BCC lt
        LDI 7
        JMP stored2
lt:     LDI 8
stored2: STA flag2
        HLT

; square: A = n → A = n*n (używa mul8 z mathlib.s).
square: STA sq_n
        TAX
        LDA sq_n
        CALL mul8
        RET
sq_n:   .byte 0

s1:     .byte 0
s2:     .byte 0
flag:   .byte 0
flag2:  .byte 0

.include "mathlib.s"
