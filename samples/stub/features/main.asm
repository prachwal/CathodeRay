; Showcase mechanik asemblera CathodeRay na CPU stub: .include, .define
; (tekstowe i funkcyjne), .macro (parametry, domyślne, .local, .paramcount),
; .if/.elseif/.else, .ifblank, tanie etykiety @, zakresy .scope/.proc z ::,
; .out/.warning/.assert, .incbin, .align.
; Budowanie i uruchomienie:
;   cathode asm samples/stub/features/main.asm --cpu stub -o /tmp/feat.bin -l /tmp/feat.lst
;   cathode stub run /tmp/feat.bin --dump 0x40:8
; Segmenty i konsolidacja modułów w podkatalogu link/.
;
; Wynik: total = 20 (suma 1+2+3+4 podwojona przez MODE), variant = 1,
; doubled = 4, z1 = 0, z2 = 5, pcc = 2.

.include "defs.inc"
.define LIMIT N
.define DOUBLE(x) (x) + (x)

.out "assembling features"
.warning "demo build"
.assert N = 4, error, "N must be 4"
.assert DOUBLE(2) = 4, error, "double broke"

.macro maybe dst, v
.ifblank v
LDI 0
.else
LDI v
.endif
STA dst
.endmacro

.macro counted a, b
LDI .paramcount
STA data::pcc
.endmacro

start:
LDX 0
TXA
loop:
ADD data::table,X
INX
CPX LIMIT
BNE loop
STA data::total
.if MODE = 1
ADD 10
.elseif MODE = 2
ADD 0
.else
SUB 1
.endif
STA data::total
.if MODE = 1
LDI 1
.elseif MODE = 2
LDI 2
.else
LDI 3
.endif
STA data::variant
LDI DOUBLE(2)
STA data::doubled
maybe data::z1
maybe data::z2, 5
counted 9, 8
LDX 3
CALL spin
HLT

.proc spin
@x: DEX
BNE @x
RET
.endproc

.scope data
table:
.incbin "blob.bin"
.align 4
total: .byte 0
variant: .byte 0
doubled: .byte 0
z1: .byte 0
z2: .byte 0
pcc: .byte 0
.endscope
