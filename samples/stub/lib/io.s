; Konsola stub (leafy, statyczne komórki jak mathlib.s).
; Decyzja I/O (plan 22): stub nie ma urządzeń, więc konsola to umowa testowa —
; stały bufor __io_buf (256 B) + kursor __io_cur. putchar dopisuje znak,
; puthex dopisuje bajt jako 2 znaki hex. Test czyta bufor z szyny.
; Wciągana przez .include (nie uruchamiaj samodzielnie):
;   .include "io.s"
;   LDI 65
;   CALL putchar   ; __io_buf[0] = 'A'
; C woła przez prototypy (putchar/puthex biorą uchar w A).
; Niszczy A/X. Rejestr SP zrównoważony.
; Segmentowe (CODE/DATA) — dokleja się do wyjścia codegenu i crt0.

.segment "CODE"

.global putchar
.global puthex

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

.segment "DATA"
cc_io_c: .byte 0
cc_io_h: .byte 0
__io_cur: .byte 0
__io_buf: .res 256
