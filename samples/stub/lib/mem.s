; Biblioteka pamięci/stringów stub (leafy, statyczne komórki jak mathlib.s).
; Wciągana przez .include (nie uruchamiaj samodzielnie):
;   .include "mem.s"
; C jeszcze nie woła (brak wskaźników w v1) — woła sterownik asm.
; ABI: adresy w cc_mm_dst/_h i cc_mm_src/_h, długość w X.
; Limity: memcpy/memset 0..255 (0 = brak), stringi < 256 znaków (licznik X).
; Niszczy A/X. Rejestr SP zrównoważony.
; Segmentowe (CODE/DATA) — dokleja się do wyjścia codegenu i crt0.

.segment "CODE"

.global mm_memcpy
.global mm_memset
.global mm_strlen
.global mm_strcpy

; memcpy: kopiuje X bajtów [src] → [dst]. X = 0: nic.
mm_memcpy:
TXA
STA mm_tmp
LDA cc_mm_src
STA mm_cpy_ld+1
LDA cc_mm_src_h
STA mm_cpy_ld+2
LDA cc_mm_dst
STA mm_cpy_st+1
LDA cc_mm_dst_h
STA mm_cpy_st+2
LDA mm_tmp
TAX
CPX 0
BEQ mm_cpy_done
mm_cpy_loop:
DEX
mm_cpy_ld: LDA 0,X
mm_cpy_st: STA 0,X
CPX 0
BNE mm_cpy_loop
mm_cpy_done:
RET

; memset: X bajtów od [dst] = A. X = 0: nic.
mm_memset:
STA mm_tmp
LDA cc_mm_dst
STA mm_set_st+1
LDA cc_mm_dst_h
STA mm_set_st+2
CPX 0
BEQ mm_set_done
mm_set_loop:
DEX
LDA mm_tmp
mm_set_st: STA 0,X
CPX 0
BNE mm_set_loop
mm_set_done:
RET

; strlen: A = długość NUL-stringa od [src] (bez NULa).
mm_strlen:
LDA cc_mm_src
STA mm_str_ld+1
LDA cc_mm_src_h
STA mm_str_ld+2
LDX 0
mm_str_loop:
mm_str_ld: LDA 0,X
BEQ mm_str_done
INX
BNE mm_str_loop
mm_str_done:
TXA
RET

; strcpy: kopiuje NUL-string [src] → [dst] (z NULem).
mm_strcpy:
LDA cc_mm_src
STA mm_scp_ld+1
LDA cc_mm_src_h
STA mm_scp_ld+2
LDA cc_mm_dst
STA mm_scp_st+1
LDA cc_mm_dst_h
STA mm_scp_st+2
LDX 0
mm_scp_loop:
mm_scp_ld: LDA 0,X
mm_scp_st: STA 0,X
BEQ mm_scp_done
INX
BNE mm_scp_loop
mm_scp_done:
RET

.segment "DATA"
cc_mm_dst: .byte 0
cc_mm_dst_h: .byte 0
cc_mm_src: .byte 0
cc_mm_src_h: .byte 0
mm_tmp: .byte 0
