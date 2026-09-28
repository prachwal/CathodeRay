; Biblioteka arytmetyczna stub: mnożenie 8x8 (mod 256) pętlą na ADD.
; Wciągana przez .include (nie uruchamiaj samodzielnie):
;   .include "mathlib.s"
;   LDI 6
;   LDX 7
;   CALL mul8    ; A = 42
; Dzielenie/modulo celowo brak: stub nie ma skoku od carry, więc nie da się
; wykryć pożyczki (propozycja: BCS/BCC jako follow-up, zob. plan 14).
; Niszczy X. Rejestr SP zrównoważony.
;
; Uwaga: stub nie ma ADD a16, więc ADD mały-adres to natychmiastowy!
; Dlatego dostęp do komórek przez tryb indeksowany z X = 0.

.global mul8

mul8:   STA mul_a
        TXA
        STA mul_b
        LDX 0
        LDI 0
        STA mul_acc
mul_loop:
        LDA mul_b
        BEQ mul_done
        SUB 1
        STA mul_b
        LDA mul_acc
        ADD mul_a,X
        STA mul_acc
        JMP mul_loop
mul_done:
        LDA mul_acc
        RET

mul_acc: .byte 0
mul_a:   .byte 0
mul_b:   .byte 0
