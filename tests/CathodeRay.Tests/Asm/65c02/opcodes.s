; Wygenerowane przez tools/make_asm_golden.py z ca65 --cpu 65C02
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
tsb $12
rmb0 $12
tsb $1234
self_bbr0: bbr0 $12,self_bbr0
ora ($12)
trb $12
rmb1 $12
inc a
trb $1234
self_bbr1: bbr1 $12,self_bbr1
rmb2 $12
self_bbr2: bbr2 $12,self_bbr2
and ($12)
bit $12,x
rmb3 $12
dec a
bit $1234,x
self_bbr3: bbr3 $12,self_bbr3
rmb4 $12
self_bbr4: bbr4 $12,self_bbr4
eor ($12)
rmb5 $12
phy
self_bbr5: bbr5 $12,self_bbr5
stz $12
rmb6 $12
self_bbr6: bbr6 $12,self_bbr6
adc ($12)
stz $12,x
rmb7 $12
ply
jmp ($1234,x)
self_bbr7: bbr7 $12,self_bbr7
bra *+5
smb0 $12
bit #$12
self_bbs0: bbs0 $12,self_bbs0
sta ($12)
smb1 $12
stz $1234
stz $1234,x
self_bbs1: bbs1 $12,self_bbs1
smb2 $12
self_bbs2: bbs2 $12,self_bbs2
lda ($12)
smb3 $12
self_bbs3: bbs3 $12,self_bbs3
smb4 $12
wai
self_bbs4: bbs4 $12,self_bbs4
cmp ($12)
smb5 $12
phx
stp
self_bbs5: bbs5 $12,self_bbs5
smb6 $12
self_bbs6: bbs6 $12,self_bbs6
sbc ($12)
smb7 $12
plx
self_bbs7: bbs7 $12,self_bbs7
