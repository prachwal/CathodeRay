; Konsola stub (leafy, statyczne komórki jak mathlib.s).
; Decyzja I/O (plan 22): stub nie ma urządzeń, więc konsola to umowa testowa —
; stały bufor __io_buf (256 B) + kursor __io_cur. putchar dopisuje znak,
; puthex dopisuje bajt jako 2 znaki hex, putdec liczbę int dziesiętnie.
; Test czyta bufor z szyny.
; Wciągana przez .include (nie uruchamiaj samodzielnie):
;   .include "io.s"
;   LDI 65
;   CALL putchar   ; __io_buf[0] = 'A'
; C woła przez prototypy (putchar/puthex biorą uchar w A, putdec int w A/X).
; Niszczy A/X. Rejestr SP zrównoważony.
; Segmentowe (CODE/DATA) — dokleja się do wyjścia codegenu i crt0.

.segment "CODE"

.global putchar
.global puthex
.global putdec

; putchar: A = znak → __io_buf[cur++].
putchar:
STA cc_io_c
LDA __io_cur
TAX
LDA cc_io_c
STA __io_buf,X
LDA __io_cur
INC
STA __io_cur
RET

; puthex: A = bajt → 2 znaki hex (starsza/młodsza tetrada).
puthex:
STA cc_io_h
SHR
SHR
SHR
SHR
CALL io_nibble
LDA cc_io_h
AND 15
CALL io_nibble
RET

; io_nibble: A = tetrada 0..15 → putchar(hex).
io_nibble:
AND 15
ADD 48
CPA 58
BCC io_nib_done
ADD 7
io_nib_done:
CALL putchar
RET

; putdec: A/X = int ze znakiem → cyfry dziesiętne (znak + cyfry).
; Dzielenie 16-bit przez odejmowanie (jak divmod); -32768 działa
; (negacja wrapuje, dzielenie bez znaku). Cyfry max 5 + bufor 6.
putdec:
STA cc_io_n
TXA
STA cc_io_n_h
LDA cc_io_n_h
AND 128
BEQ dec_pos        ; hi bit7 = 0: dodatnia
LDA cc_io_n
NOT
STA cc_io_n
LDA cc_io_n_h
NOT
STA cc_io_n_h
LDA cc_io_n
INC
STA cc_io_n
BNE dec_nobrw
LDA cc_io_n_h
INC
STA cc_io_n_h
dec_nobrw:
LDI 45
CALL putchar       ; '-'
dec_pos:
LDX 0
LDA cc_io_n
ORA cc_io_n_h,X
BEQ dec_zero       ; n == 0: samo '0'
dec_div:
LDI 0
STA cc_io_q
STA cc_io_q_h
dec_loop:
LDA cc_io_n_h
BNE dec_sub        ; hi > 0: na pewno >= 10
LDA cc_io_n
CPA 10
BCC dec_got        ; lo < 10: koniec
dec_sub:
LDA cc_io_n
SUB 10
STA cc_io_n
BCS dec_nobrw2
LDA cc_io_n_h
SUB 1
STA cc_io_n_h
dec_nobrw2:
LDA cc_io_q
ADD 1
STA cc_io_q
BCC dec_loop
LDA cc_io_q_h
ADD 1
STA cc_io_q_h
JMP dec_loop
dec_got:
LDX 0
LDA cc_io_cnt
TAX
LDA cc_io_n
STA cc_io_d,X      ; cyfra (reszta)
LDA cc_io_cnt
ADD 1
STA cc_io_cnt
LDA cc_io_q
STA cc_io_n
LDA cc_io_q_h
STA cc_io_n_h
LDX 0
LDA cc_io_q
ORA cc_io_q_h,X
BNE dec_div        ; iloraz > 0: kolejna cyfra
dec_prt:
LDA cc_io_cnt
BEQ dec_done
SUB 1
TAX
STA cc_io_cnt
LDA cc_io_d,X
ADD 48
CALL putchar
JMP dec_prt
dec_done:
RET
dec_zero:
LDI 48
CALL putchar
RET

.segment "DATA"
cc_io_c: .byte 0
cc_io_h: .byte 0
cc_io_n: .byte 0
cc_io_n_h: .byte 0
cc_io_q: .byte 0
cc_io_q_h: .byte 0
cc_io_cnt: .byte 0
cc_io_d: .byte 0
cc_io_d1: .byte 0
cc_io_d2: .byte 0
cc_io_d3: .byte 0
cc_io_d4: .byte 0
cc_io_d5: .byte 0
__io_cur: .byte 0
__io_buf: .res 256
