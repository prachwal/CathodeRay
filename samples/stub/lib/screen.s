; Ekran tekstowy stub 40x25 (1000 B, wierszami).
; Bufor __scr_buf + kursor __scr_cur/_h (0..999, 1000 = za końcem).
; scr_putc dopisuje znak (A); 10 to nowa linia (wiersz+1, na dole scroll);
; pełny ekran scrolluje w górę (ostatni wiersz zerowany, kursor na 960).
; scr_goto ustawia kursor (A = x 0..39, X = y 0..24, poza zakresem tnie).
; scr_clear zeruje bufor i kursor. Leafy (scroll/goto wołane tylko z putc),
; statyczne komórki jak mathlib.s. Wciągana przez .include:
;   .include "screen.s"
;   LDI 65
;   CALL scr_putc   ; __scr_buf[0] = 'A'
; C woła przez prototypy (uchar). Strony wybierane skokami (X ma 8 bitów,
; więc po 256 B osobna etykieta z operandem __scr_buf+N — linker dolicza).
; Brak przepełnienia: kursor nigdy > 1000, bufor nigdy poza 1000 B
; (clampy + scroll; dzielenie/modulo omijane odejmowaniem).
; Niszczy A/X. Rejestr SP zrównoważony.
; Segmentowe (CODE/DATA) — dokleja się do wyjścia codegenu i crt0.

.segment "CODE"

.global scr_putc
.global scr_goto
.global scr_clear
.global __scr_buf
.global __scr_cur

; scr_putc: A = znak (10 = nowa linia) -> __scr_buf[cur++].
scr_putc:
STA cc_sc_c
CPA 10
BNE putc_go
LDA __scr_cur
STA cc_sc_nl
LDA __scr_cur_h
STA cc_sc_nl_h
LDI 0
STA cc_sc_rw
nl_row:
LDA cc_sc_nl_h
BNE nl_sub          ; hi != 0: na pewno >= 40
LDA cc_sc_nl
CPA 40
BCC nl_done         ; lo < 40: wiersz znaleziony
nl_sub:
LDA cc_sc_nl
SUB 40
STA cc_sc_nl
BCS nl_nobrw        ; C=1: bez pożyczki
LDA cc_sc_nl_h
SUB 1
STA cc_sc_nl_h
nl_nobrw:
LDA cc_sc_rw
ADD 1
STA cc_sc_rw
JMP nl_row
nl_done:
LDA cc_sc_rw
CPA 24
BCC nl_goto         ; wiersz < 24: skok
CALL sc_scroll      ; wiersz 24+: scroll (kursor na 960)
RET
nl_goto:
ADD 1
TAX
LDI 0
CALL scr_goto
RET
putc_go:
LDA __scr_cur_h
BEQ pg0put
CPA 1
BEQ pg1put
CPA 2
BEQ pg2put
CPA 3
BNE do_scroll
LDA __scr_cur
CPA 232            ; 1000 = 3*256+232
BCS do_scroll
TAX
LDA cc_sc_c
STA __scr_buf+768,X
JMP sc_inc
pg0put:
LDA __scr_cur
TAX
LDA cc_sc_c
sc_w0: STA __scr_buf,X
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
do_scroll:
CALL sc_scroll
JMP putc_go
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

; scr_goto: A = x (tniemy do 39), X = y (tniemy do 24) -> kursor = y*40+x.
scr_goto:
STA cc_sc_x
TXA
STA cc_sc_t
CPA 25
BCC gy_ok
LDI 24
STA cc_sc_t
gy_ok:
LDA cc_sc_x
CPA 40
BCC gx_ok
LDI 39
STA cc_sc_x
gx_ok:
LDX 0
LDA cc_sc_t
SHL
SHL
SHL
STA cc_sc_q        ; y*8 (y <= 24, bez carry)
STA cc_sc_p
LDI 0
STA cc_sc_p_h
LDA cc_sc_p
SHL
STA cc_sc_p
LDA cc_sc_p_h
ADC cc_sc_p_h,X
STA cc_sc_p_h
LDA cc_sc_p
SHL
STA cc_sc_p
LDA cc_sc_p_h
ADC cc_sc_p_h,X
STA cc_sc_p_h      ; p = y*32
LDA cc_sc_p
ADD cc_sc_q,X
ADD cc_sc_x,X
STA __scr_cur
LDA cc_sc_p_h
ADC 0
STA __scr_cur_h    ; cur = y*32 + y*8 + x
RET

; sc_scroll: wiersze 1..24 w górę, wiersz 24 zerowany, kursor = 960.
; Adres bazowy czytany z operandu sc_w0 (linker go wypełnia).
sc_scroll:
LDX 0
LDA sc_w0+1
ADD 40
STA cc_sc_src
LDA sc_w0+2
ADC 0
STA cc_sc_src_h
LDA sc_w0+1
STA cc_sc_dst
LDA sc_w0+2
STA cc_sc_dst_h
LDI 24
STA cc_sc_r
sc_row:
LDA cc_sc_src
STA sc_mv_ld+1
LDA cc_sc_src_h
STA sc_mv_ld+2
LDA cc_sc_dst
STA sc_mv_st+1
LDA cc_sc_dst_h
STA sc_mv_st+2
LDX 0
sc_byte:
sc_mv_ld: LDA 0,X
sc_mv_st: STA 0,X
INX
CPX 40
BNE sc_byte
LDX 0
LDA cc_sc_src
ADD 40
STA cc_sc_src
LDA cc_sc_src_h
ADC 0
STA cc_sc_src_h
LDA cc_sc_dst
ADD 40
STA cc_sc_dst
LDA cc_sc_dst_h
ADC 0
STA cc_sc_dst_h
LDA cc_sc_r
SUB 1
STA cc_sc_r
BNE sc_row
LDI 0
LDX 0
sc_clr:
STA sc_mv_st,X
INX
CPX 40
BNE sc_clr
LDI 192            ; 960 = 0x03C0
STA __scr_cur
LDI 3
STA __scr_cur_h
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
cc_sc_x: .byte 0
cc_sc_t: .byte 0
cc_sc_q: .byte 0
cc_sc_p: .byte 0
cc_sc_p_h: .byte 0
cc_sc_nl: .byte 0
cc_sc_nl_h: .byte 0
cc_sc_rw: .byte 0
cc_sc_src: .byte 0
cc_sc_src_h: .byte 0
cc_sc_dst: .byte 0
cc_sc_dst_h: .byte 0
cc_sc_r: .byte 0
__scr_cur: .byte 0
__scr_cur_h: .byte 0
__scr_buf: .res 1024   ; 1024 (nie 1000): pełne pętle X po 256, używane 1000
