; Wygenerowane przez tools/make_asm_golden.py z ca65 --cpu 6502
.org $0600
brk
ora ($12,x)
ora $12
asl $12
php
ora #$12
asl a
ora $1234
asl $1234
bpl *+5
ora ($12),y
ora $12,x
asl $12,x
clc
ora $1234,y
nop
ora $1234,x
asl $1234,x
jsr $1234
and ($12,x)
bit $12
and $12
rol $12
plp
and #$12
rol a
bit $1234
and $1234
rol $1234
bmi *+5
and ($12),y
and $12,x
rol $12,x
sec
and $1234,y
and $1234,x
rol $1234,x
rti
eor ($12,x)
eor $12
lsr $12
pha
eor #$12
lsr a
jmp $1234
eor $1234
lsr $1234
bvc *+5
eor ($12),y
eor $12,x
lsr $12,x
cli
eor $1234,y
eor $1234,x
lsr $1234,x
rts
adc ($12,x)
adc $12
ror $12
pla
adc #$12
ror a
jmp ($1234)
adc $1234
ror $1234
bvs *+5
adc ($12),y
adc $12,x
ror $12,x
sei
adc $1234,y
adc $1234,x
ror $1234,x
sta ($12,x)
sty $12
sta $12
stx $12
dey
txa
sty $1234
sta $1234
stx $1234
bcc *+5
sta ($12),y
sty $12,x
sta $12,x
stx $12,y
tya
sta $1234,y
txs
sta $1234,x
ldy #$12
lda ($12,x)
ldx #$12
ldy $12
lda $12
ldx $12
tay
lda #$12
tax
ldy $1234
lda $1234
ldx $1234
bcs *+5
lda ($12),y
ldy $12,x
lda $12,x
ldx $12,y
clv
lda $1234,y
tsx
ldy $1234,x
lda $1234,x
ldx $1234,y
cpy #$12
cmp ($12,x)
cpy $12
cmp $12
dec $12
iny
cmp #$12
dex
cpy $1234
cmp $1234
dec $1234
bne *+5
cmp ($12),y
cmp $12,x
dec $12,x
cld
cmp $1234,y
cmp $1234,x
dec $1234,x
cpx #$12
sbc ($12,x)
cpx $12
sbc $12
inc $12
inx
sbc #$12
cpx $1234
sbc $1234
inc $1234
beq *+5
sbc ($12),y
sbc $12,x
inc $12,x
sed
sbc $1234,y
sbc $1234,x
inc $1234,x
