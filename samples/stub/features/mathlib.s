; Biblioteka arytmetyczna stub: mnożenie i dzielenie 8-bitowe pętlą na ADD/SUB.
; Wciągana przez .include (nie uruchamiaj samodzielnie):
;   .include "mathlib.s"
;   LDI 6
;   LDX 7
;   CALL mul8     ; A = 42
;   LDI 42
;   LDX 5
;   CALL divmod   ; A = 8, X = 2
; Niszczy X. Rejestr SP zrównoważony.
;
; Uwaga: stub nie ma ADD/SUB a16, więc mały-adres to natychmiastowy!
; Dlatego dostęp do komórek przez tryb indeksowany z X = 0.

.global mul8
.global divmod

mul8:   STA mul_a
        TXA
        STA mul_b
        LDX 0
        LDI 0
        STA mul_acc
mul_loop: LDA mul_b
        BEQ mul_done
        SUB 1
        STA mul_b
        LDA mul_acc
        ADD mul_a,X
        STA mul_acc
        JMP mul_loop
mul_done: LDA mul_acc
        RET

mul_acc: .byte 0
mul_a:   .byte 0
mul_b:   .byte 0

; Dzielenie całkowite: A = n, X = d → A = n/d (floor), X = n%d.
; Dzielnik 0 daje A = X = 0 (nie wiesza się). Niszczy X.
; Odejmowanie w pętli wykrywa pożyczkę przez BCC; operand SUB łatany
; (stub nie ma SUB a16 ani SUB a16,X, więc dzielnik ląduje w kodzie).
divmod:
CPX 0
BEQ div_zero
STA div_n
TXA
STA div_op+1
LDX 0
LDI 0
STA div_q
div_loop: LDA div_n
div_op: SUB 0
        BCC div_done
        STA div_n
        LDA div_q
        INC
        STA div_q
        JMP div_loop
div_done: LDA div_n
        TAX
        LDA div_q
        RET
div_zero: LDI 0
        TAX
        RET

div_n:   .byte 0
div_q:   .byte 0
