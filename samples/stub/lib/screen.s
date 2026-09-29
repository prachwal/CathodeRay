; Ekran tekstowy stub 40x25 (1000 B, wierszami).
; Bufor __scr_buf + kursor __scr_cur/_h (0..999). scr_putc dopisuje znak
; (A), po 999 gubi; scr_clear zeruje bufor i kursor. Leafy, statyczne
; komórki jak mathlib.s. Wciągana przez .include:
;   .include "screen.s"
;   LDI 65
;   CALL scr_putc   ; __scr_buf[0] = 'A'
; C woła przez prototypy (uchar w A). Strony wybierane skokami (X ma 8 bitów,
; więc po 256 B osobna etykieta z operandem __scr_buf+N — linker dolicza).
; Niszczy A/X. Rejestr SP zrównoważony.
; Segmentowe (CODE/DATA) — dokleja się do wyjścia codegenu i crt0.

.segment "CODE"

.global scr_putc
.global scr_clear
.global __scr_buf
.global __scr_cur

; scr_putc: A = znak -> __scr_buf[cur++]; cur > 999: gubi.
scr_putc:
STA cc_sc_c
LDA __scr_cur_h
BEQ pg0put
CPA 1
BEQ pg1put
CPA 2
BEQ pg2put
CPA 3
BNE drop           ; hi > 3: pełny
LDA __scr_cur
CPA 232            ; 1000 = 3*256+232
BCS drop           ; lo >= 232: pełny
TAX
LDA cc_sc_c
STA __scr_buf+768,X
JMP sc_inc
pg0put:
LDA __scr_cur
TAX
LDA cc_sc_c
STA __scr_buf,X
JMP sc_inc
pg1put:
LDA __scr_cur
TAX
LDA cc_sc_c
STA __scr_buf+256,X
JMP sc_inc
pg2put:
LDA __scr_cur
TAX
LDA cc_sc_c
STA __scr_buf+512,X
JMP sc_inc
drop:
RET
sc_inc:
LDA __scr_cur
ADD 1
STA __scr_cur
BNE sc_done        ; brak zawinięcia: koniec
LDA __scr_cur_h
ADD 1
STA __scr_cur_h
sc_done:
RET

; scr_clear: bufor = 0, kursor = 0.
scr_clear:
LDI 0
STA __scr_cur
STA __scr_cur_h
TAX
clr0: STA __scr_buf,X
DEX
BNE clr0
clr1: STA __scr_buf+256,X
DEX
BNE clr1
clr2: STA __scr_buf+512,X
DEX
BNE clr2
clr3: STA __scr_buf+768,X
DEX
BNE clr3
RET

.segment "DATA"
cc_sc_c: .byte 0
__scr_cur: .byte 0
__scr_cur_h: .byte 0
__scr_buf: .res 1000
