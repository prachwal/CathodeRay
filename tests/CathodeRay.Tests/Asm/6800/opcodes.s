; Wygenerowane przez tools/make_asm_golden.py z as6800
	org $0600
	nop
	tap
	tpa
	inx
	dex
	clv
	sev
	clc
	sec
	cli
	sei
	sba
	cba
	tab
	tba
	daa
	aba
	tsx
	ins
	des
	txs
	rts
	rti
	wai
	swi
	nega
	coma
	lsra
	rora
	asra
	asla
	rola
	deca
	inca
	tsta
	clra
	negb
	comb
	lsrb
	rorb
	asrb
	aslb
	rolb
	decb
	incb
	tstb
	clrb
	neg $1234
	com $1234
	lsr $1234
	ror $1234
	asr $1234
	asl $1234
	rol $1234
	dec $1234
	inc $1234
	tst $1234
	jmp $1234
	clr $1234
	neg $12,x
	com $12,x
	lsr $12,x
	ror $12,x
	asr $12,x
	asl $12,x
	rol $12,x
	dec $12,x
	inc $12,x
	tst $12,x
	jmp $12,x
	clr $12,x
	suba #$12
	cmpa #$12
	sbca #$12
	anda #$12
	bita #$12
	ldaa #$12
	eora #$12
	oraa #$12
	adda #$12
	cpx #$1234
	bsr tgt1
tgt1:
	lds #$1234
	suba $12
	cmpa $12
	sbca $12
	anda $12
	bita $12
	ldaa $12
	staa $12
	eora $12
	oraa $12
	adda $12
	cpx $12
	lds $12
	sts $12
	suba $12,x
	cmpa $12,x
	sbca $12,x
	anda $12,x
	bita $12,x
	ldaa $12,x
	staa $12,x
	eora $12,x
	oraa $12,x
	adda $12,x
	cpx $12,x
	jsr $12,x
	lds $12,x
	sts $12,x
	suba $1234
	cmpa $1234
	sbca $1234
	anda $1234
	bita $1234
	ldaa $1234
	staa $1234
	eora $1234
	oraa $1234
	adda $1234
	cpx $1234
	jsr $1234
	lds $1234
	sts $1234
	subb #$12
	cmpb #$12
	sbcb #$12
	andb #$12
	bitb #$12
	ldab #$12
	eorb #$12
	orab #$12
	addb #$12
	ldx #$1234
	subb $12
	cmpb $12
	sbcb $12
	andb $12
	bitb $12
	ldab $12
	stab $12
	eorb $12
	orab $12
	addb $12
	ldx $12
	stx $12
	subb $12,x
	cmpb $12,x
	sbcb $12,x
	andb $12,x
	bitb $12,x
	ldab $12,x
	stab $12,x
	eorb $12,x
	orab $12,x
	addb $12,x
	ldx $12,x
	stx $12,x
	subb $1234
	cmpb $1234
	sbcb $1234
	andb $1234
	bitb $1234
	ldab $1234
	stab $1234
	eorb $1234
	orab $1234
	addb $1234
	adca $1234
	ldx $1234
	stx $1234
	adca #$12
	adca $12
	adca $12,x
	adcb #$12
	adcb $12
	adcb $12,x
	adcb $1234
	bra tgt2
tgt2:
	bhi tgt3
tgt3:
	bls tgt4
tgt4:
	bcc tgt5
tgt5:
	bcs tgt6
tgt6:
	bne tgt7
tgt7:
	beq tgt8
tgt8:
	bvc tgt9
tgt9:
	bvs tgt10
tgt10:
	bpl tgt11
tgt11:
	bmi tgt12
tgt12:
	bge tgt13
tgt13:
	blt tgt14
tgt14:
	bgt tgt15
tgt15:
	ble tgt16
tgt16:
