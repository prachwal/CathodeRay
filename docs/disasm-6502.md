# Deasemblacja: nasz kompilator vs cc65 (6502)

## add32

| kompilator | bajty funkcji | instrukcje | wołane procedury zewnętrzne |
| --- | ---: | ---: | --- |
| nasz | 46 | 22 | - |
| cc65 | 22 | 12 | ldeaxysp, pusheax, tosaddeax |

stosunek nasz / cc65: 2.09

- cc65: sama funkcja 22 B; po zlinkowaniu z runtime cc65 147 B (w tym 4 B startera)

### nasz

```asm
    lda     $14
    clc
    adc     $18
    sta     $1E
    lda     $15
    adc     $19
    sta     $1F
    lda     $16
    adc     $7001
    sta     $20
    lda     $17
    adc     $7002
    sta     $21
    lda     $20
    sta     $8040
    lda     $21
    sta     $8041
    lda     $1E
    sta     $1A
    lda     $1F
    sta     $1B
    rts
```

### cc65

```asm
    L1035           := $1035
    L1040           := $1040
    L1061           := $1061
    L107B           := $107B
    jsr     pusheax
    ldy     #$07
    jsr     ldeaxysp
    jsr     pusheax
    ldy     #$07
    jsr     ldeaxysp
    jsr     tosaddeax
    jmp     incsp8
```

## bubble

| kompilator | bajty funkcji | instrukcje | wołane procedury zewnętrzne |
| --- | ---: | ---: | --- |
| nasz | 446 | 234 | - |
| cc65 | 379 | 217 | decsp6, pushax, pushwysp, stax0sp, staxspidx, staxysp, tosaddax, tosicmp |

stosunek nasz / cc65: 1.18

- cc65: sama funkcja 379 B; po zlinkowaniu z runtime cc65 598 B (w tym 4 B startera)

### nasz

```asm
    lda     #$00
    sta     $1E
    sta     $1F
L1080:  lda     $16
    sec
    sbc     #$01
    sta     $26
    lda     $17
    sbc     #$00
    sta     $27
    lda     $1F
    eor     #$80
    sta     $1C
    lda     $27
    eor     #$80
    sta     $1D
    lda     $1E
    sec
    sbc     $26
    lda     $1C
    sbc     $1D
    bcc     L10A7
    jmp     L1237
L10A7:  lda     #$00
    sta     $20
    sta     $21
L10AD:  lda     $16
    sec
    sbc     #$01
    sta     $26
    lda     $17
    sbc     #$00
    sta     $27
    lda     $26
    sec
    sbc     $1E
    sta     $26
    lda     $27
    sbc     $1F
    sta     $27
    lda     $21
    eor     #$80
    sta     $1C
    lda     $27
    eor     #$80
    sta     $1D
    lda     $20
    sec
    sbc     $26
    lda     $1C
    sbc     $1D
    bcc     L10E1
    jmp     L122E
L10E1:  lda     $20
    sta     $26
    lda     $21
    sta     $27
    lda     $26
    asl     a
    sta     $26
    lda     $27
    rol     a
    sta     $27
    lda     $14
    clc
    adc     $26
    sta     $24
    lda     $15
    adc     $27
    sta     $25
    lda     $24
    sta     $10
    lda     $25
    sta     $11
    ldy     #$00
    lda     ($10),y
    sta     $24
    ldy     #$01
    lda     ($10),y
    sta     $25
    lda     $20
    clc
    adc     #$01
    sta     $28
    lda     $21
    adc     #$00
    sta     $29
    lda     $28
    asl     a
    sta     $28
    lda     $29
    rol     a
    sta     $29
    lda     $14
    clc
    adc     $28
    sta     $26
    lda     $15
    adc     $29
    sta     $27
    lda     $26
    sta     $10
    lda     $27
    sta     $11
    ldy     #$00
    lda     ($10),y
    sta     $26
    ldy     #$01
    lda     ($10),y
    sta     $27
    eor     #$80
    sta     $1C
    lda     $25
    eor     #$80
    sta     $1D
    lda     $26
    sec
    sbc     $24
    lda     $1C
    sbc     $1D
    bcc     L1164
    jmp     L1225
L1164:  lda     $20
    sta     $26
    lda     $21
    sta     $27
    lda     $26
    asl     a
    sta     $26
    lda     $27
    rol     a
    sta     $27
    lda     $14
    clc
    adc     $26
    sta     $24
    lda     $15
    adc     $27
    sta     $25
    ldy     #$00
    lda     ($24),y
    sta     $22
    ldy     #$01
    lda     ($24),y
    sta     $23
    lda     $20
    clc
    adc     #$01
    sta     $26
    lda     $21
    adc     #$00
    sta     $27
    lda     $26
    asl     a
    sta     $26
    lda     $27
    rol     a
    sta     $27
    lda     $14
    clc
    adc     $26
    sta     $24
    lda     $15
    adc     $27
    sta     $25
    lda     $24
    sta     $10
    lda     $25
    sta     $11
    ldy     #$00
    lda     ($10),y
    sta     $24
    ldy     #$01
    lda     ($10),y
    sta     $25
    lda     $20
    sta     $28
    lda     $21
    sta     $29
    lda     $28
    asl     a
    sta     $28
    lda     $29
    rol     a
    sta     $29
    lda     $14
    clc
    adc     $28
    sta     $26
    lda     $15
    adc     $29
    sta     $27
    lda     $24
    ldy     #$00
    sta     ($26),y
    lda     $25
    ldy     #$01
    sta     ($26),y
    lda     $20
    clc
    adc     #$01
    sta     $28
    lda     $21
    adc     #$00
    sta     $29
    lda     $28
    asl     a
    sta     $28
    lda     $29
    rol     a
    sta     $29
    lda     $14
    clc
    adc     $28
    sta     $26
    lda     $15
    adc     $29
    sta     $27
    lda     $22
    ldy     #$00
    sta     ($26),y
    lda     $23
    ldy     #$01
    sta     ($26),y
    jmp     L1225
L1225:  inc     $20
    bne     L122B
    inc     $21
L122B:  jmp     L10AD
L122E:  inc     $1E
    bne     L1234
    inc     $1F
L1234:  jmp     L1080
L1237:  rts
```

### cc65

```asm
    L1181           := $1181
    L119C           := $119C
    L11A9           := $11A9
    L11B8           := $11B8
    L11FE           := $11FE
    L1216           := $1216
    L1230           := $1230
    L1232           := $1232
    L123B           := $123B
    jsr     pushax
    jsr     decsp6
    ldy     #$04
    lda     #$00
    sta     ($00),y
    iny
    sta     ($00),y
L1013:  ldy     #$07
    jsr     pushwysp
    ldy     #$09
    lda     ($00),y
    tax
    dey
    lda     ($00),y
    sec
    sbc     #$01
    bcs     L1026
    dex
L1026:  jsr     tosicmp
    bmi     L102E
    jmp     L117A
L102E:  ldy     #$02
    lda     #$00
    sta     ($00),y
    iny
    sta     ($00),y
L1037:  ldy     #$05
    jsr     pushwysp
    ldy     #$09
    lda     ($00),y
    tax
    dey
    lda     ($00),y
    sec
    sbc     #$01
    bcs     L104B
    dex
    sec
L104B:  ldy     #$06
    sbc     ($00),y
    pha
    txa
    iny
    sbc     ($00),y
    tax
    pla
    jsr     tosicmp
    bmi     L105E
    jmp     L1166
L105E:  ldy     #$0B
    jsr     pushwysp
    ldy     #$05
    lda     ($00),y
    sta     $10
    dey
    lda     ($00),y
    asl     a
    rol     $10
    ldx     $10
    jsr     tosaddax
    sta     $08
    stx     $09
    ldy     #$01
    lda     ($08),y
    tax
    dey
    lda     ($08),y
    jsr     pushax
    ldy     #$05
    lda     ($00),y
    tax
    dey
    lda     ($00),y
    clc
    adc     #$01
    bcc     L1091
    inx
L1091:  stx     $10
    asl     a
    rol     $10
    clc
    ldy     #$0A
    adc     ($00),y
    sta     $08
    lda     $10
    iny
    adc     ($00),y
    sta     $09
    ldy     #$01
    lda     ($08),y
    tax
    dey
    lda     ($08),y
    jsr     tosicmp
    bpl     L10B4
    jmp     L1152
L10B4:  bne     L10B9
    jmp     L1152
L10B9:  ldy     #$0B
    jsr     pushwysp
    ldy     #$05
    lda     ($00),y
    sta     $10
    dey
    lda     ($00),y
    asl     a
    rol     $10
    ldx     $10
    jsr     tosaddax
    sta     $08
    stx     $09
    ldy     #$01
    lda     ($08),y
    tax
    dey
    lda     ($08),y
    jsr     stax0sp
    ldy     #$0B
    jsr     pushwysp
    ldy     #$05
    lda     ($00),y
    sta     $10
    dey
    lda     ($00),y
    asl     a
    rol     $10
    ldx     $10
    jsr     tosaddax
    jsr     pushax
    ldy     #$05
    lda     ($00),y
    tax
    dey
    lda     ($00),y
    clc
    adc     #$01
    bcc     L1105
    inx
L1105:  stx     $10
    asl     a
    rol     $10
    clc
    ldy     #$0A
    adc     ($00),y
    sta     $08
    lda     $10
    iny
    adc     ($00),y
    sta     $09
    ldy     #$01
    lda     ($08),y
    tax
    dey
    lda     ($08),y
    jsr     staxspidx
    ldy     #$03
    lda     ($00),y
    tax
    dey
    lda     ($00),y
    clc
    adc     #$01
    bcc     L1131
    inx
L1131:  stx     $10
    asl     a
    rol     $10
    clc
    ldy     #$08
    adc     ($00),y
    sta     $08
    lda     $10
    iny
    adc     ($00),y
    sta     $09
    ldy     #$01
    lda     ($00),y
    tax
    dey
    lda     ($00),y
    sta     ($08),y
    iny
    txa
    sta     ($08),y
L1152:  ldy     #$03
    lda     ($00),y
    tax
    dey
    lda     ($00),y
    clc
    adc     #$01
    bcc     L1160
    inx
L1160:  jsr     staxysp
    jmp     L1037
L1166:  ldy     #$05
    lda     ($00),y
    tax
    dey
    lda     ($00),y
    clc
    adc     #$01
    bcc     L1174
    inx
L1174:  jsr     staxysp
    jmp     L1013
L117A:  ldy     #$0A
    jmp     addysp
```

## copy

| kompilator | bajty funkcji | instrukcje | wołane procedury zewnętrzne |
| --- | ---: | ---: | --- |
| nasz | 42 | 21 | - |
| cc65 | 92 | 55 | pushax, pushwysp, staspidx, stax0sp, staxysp |

stosunek nasz / cc65: 0.46

- cc65: sama funkcja 92 B; po zlinkowaniu z runtime cc65 224 B (w tym 4 B startera)

### nasz

```asm
L107A:  lda     $18
    ora     $19
    beq     L10A3
    ldy     #$00
    lda     ($16),y
    sta     $1E
    lda     $1E
    ldy     #$00
    sta     ($14),y
    inc     $14
    bne     L1092
    inc     $15
L1092:  inc     $16
    bne     L1098
    inc     $17
L1098:  lda     $18
    bne     L109E
    dec     $19
L109E:  dec     $18
    jmp     L107A
L10A3:  rts
```

### cc65

```asm
    L1084           := $1084
    L108D           := $108D
    L10A5           := $10A5
    L10BF           := $10BF
    L10D5           := $10D5
    L10D7           := $10D7
    jsr     pushax
    jmp     L1054
L100A:  ldy     #$07
    jsr     pushwysp
    ldy     #$05
    lda     ($00),y
    sta     $09
    dey
    lda     ($00),y
    sta     $08
    ldy     #$00
    lda     ($08),y
    jsr     staspidx
    ldy     #$05
    lda     ($00),y
    tax
    dey
    lda     ($00),y
    clc
    adc     #$01
    bcc     L102F
    inx
L102F:  jsr     staxysp
    ldy     #$03
    lda     ($00),y
    tax
    dey
    lda     ($00),y
    clc
    adc     #$01
    bcc     L1040
    inx
L1040:  jsr     staxysp
    ldy     #$01
    lda     ($00),y
    tax
    dey
    lda     ($00),y
    sec
    sbc     #$01
    bcs     L1051
    dex
L1051:  jsr     stax0sp
L1054:  ldy     #$01
    lda     ($00),y
    dey
    ora     ($00),y
    bne     L100A
    jmp     incsp6
```

## div16

| kompilator | bajty funkcji | instrukcje | wołane procedury zewnętrzne |
| --- | ---: | ---: | --- |
| nasz | 92 | 48 | - |
| cc65 | 44 | 26 | pushax, pushwysp, tosaddax, tosdivax, tosmodax |

stosunek nasz / cc65: 2.09

- cc65: sama funkcja 44 B; po zlinkowaniu z runtime cc65 352 B (w tym 4 B startera)

### nasz

```asm
    L10DD           := $10DD
    L119A           := $119A
    lda     $14
    sta     $1E
    lda     $15
    sta     $1F
    lda     $16
    sta     $20
    lda     $17
    sta     $21
    lda     $1E
    sta     $14
    lda     $1F
    sta     $15
    lda     $20
    sta     $16
    lda     $21
    sta     $17
    jsr     L10DD
    lda     $1A
    sta     $22
    lda     $1B
    sta     $23
    lda     $1E
    sta     $14
    lda     $1F
    sta     $15
    lda     $20
    sta     $16
    lda     $21
    sta     $17
    jsr     L119A
    lda     $1A
    sta     $24
    lda     $1B
    sta     $25
    lda     $22
    clc
    adc     $24
    sta     $22
    lda     $23
    adc     $25
    sta     $23
    lda     $22
    sta     $1A
    lda     $23
    sta     $1B
    rts
```

### cc65

```asm
    L1032           := $1032
    L105C           := $105C
    L1085           := $1085
    L108C           := $108C
    L10BF           := $10BF
    L10D7           := $10D7
    jsr     pushax
    ldy     #$05
    jsr     pushwysp
    ldy     #$03
    lda     ($00),y
    tax
    dey
    lda     ($00),y
    jsr     tosdivax
    jsr     pushax
    ldy     #$07
    jsr     pushwysp
    ldy     #$05
    lda     ($00),y
    tax
    dey
    lda     ($00),y
    jsr     tosmodax
    jsr     tosaddax
    jmp     incsp4
```

## fib

| kompilator | bajty funkcji | instrukcje | wołane procedury zewnętrzne |
| --- | ---: | ---: | --- |
| nasz | 144 | 77 | - |
| cc65 | 74 | 44 | _fib, pushax, tosaddax |

stosunek nasz / cc65: 1.95

- cc65: sama funkcja 74 B; po zlinkowaniu z runtime cc65 154 B (w tym 4 B startera)

### nasz

```asm
L107A:  lda     $1E
    pha
    lda     $1F
    pha
    lda     $20
    pha
    lda     $21
    pha
    lda     $14
    sta     $1E
    lda     $15
    sta     $1F
    eor     #$80
    sta     $1C
    lda     $1E
    sec
    sbc     #$02
    lda     $1C
    sbc     #$80
    bcs     L10A8
    lda     $1E
    sta     $1A
    lda     $1F
    sta     $1B
    jmp     L10FD
L10A8:  lda     $1E
    sec
    sbc     #$01
    sta     $22
    lda     $1F
    sbc     #$00
    sta     $23
    lda     $22
    sta     $14
    lda     $23
    sta     $15
    jsr     L107A
    lda     $1A
    sta     $20
    lda     $1B
    sta     $21
    lda     $1E
    sec
    sbc     #$02
    sta     $24
    lda     $1F
    sbc     #$00
    sta     $25
    lda     $24
    sta     $14
    lda     $25
    sta     $15
    jsr     L107A
    lda     $1A
    sta     $22
    lda     $1B
    sta     $23
    lda     $20
    clc
    adc     $22
    sta     $20
    lda     $21
    adc     $23
    sta     $21
    lda     $20
    sta     $1A
    lda     $21
    sta     $1B
L10FD:  pla
    sta     $21
    pla
    sta     $20
    pla
    sta     $1F
    pla
    sta     $1E
    rts
```

### cc65

```asm
    L1050           := $1050
    L1072           := $1072
    L1084           := $1084
L1004:  jsr     pushax
    ldy     #$01
    lda     ($00),y
    tax
    dey
    lda     ($00),y
    cmp     #$02
    txa
    sbc     #$00
    bvc     L1018
    eor     #$80
L1018:  bpl     L1024
    iny
    lda     ($00),y
    tax
    dey
    lda     ($00),y
    jmp     incsp2
L1024:  iny
    lda     ($00),y
    tax
    dey
    lda     ($00),y
    sec
    sbc     #$01
    bcs     L1031
    dex
L1031:  jsr     _fib
    jsr     pushax
    ldy     #$03
    lda     ($00),y
    tax
    dey
    lda     ($00),y
    sec
    sbc     #$02
    bcs     L1045
    dex
L1045:  jsr     _fib
    jsr     tosaddax
    jmp     incsp2
```

## find

| kompilator | bajty funkcji | instrukcje | wołane procedury zewnętrzne |
| --- | ---: | ---: | --- |
| nasz | 119 | 61 | - |
| cc65 | 119 | 71 | decsp2, pushax, pushwysp, stax0sp, tosaddax |

stosunek nasz / cc65: 1.00

- cc65: sama funkcja 119 B; po zlinkowaniu z runtime cc65 248 B (w tym 4 B startera)

### nasz

```asm
    lda     #$00
    sta     $1E
    sta     $1F
L1080:  lda     $1F
    eor     #$80
    sta     $1C
    lda     $17
    eor     #$80
    sta     $1D
    lda     $1E
    sec
    sbc     $16
    lda     $1C
    sbc     $1D
    bcs     L10EA
    lda     $1E
    sta     $22
    lda     $1F
    sta     $23
    lda     $22
    asl     a
    sta     $22
    lda     $23
    rol     a
    sta     $23
    lda     $14
    clc
    adc     $22
    sta     $20
    lda     $15
    adc     $23
    sta     $21
    lda     $20
    sta     $10
    lda     $21
    sta     $11
    ldy     #$00
    lda     ($10),y
    sta     $20
    ldy     #$01
    lda     ($10),y
    sta     $21
    lda     $20
    cmp     $18
    bne     L10E1
    lda     $21
    cmp     $19
    bne     L10E1
    lda     $1E
    sta     $1A
    lda     $1F
    sta     $1B
    jmp     L10F0
L10E1:  inc     $1E
    bne     L10E7
    inc     $1F
L10E7:  jmp     L1080
L10EA:  lda     #$FF
    sta     $1A
    sta     $1B
L10F0:  rts
```

### cc65

```asm
    L107D           := $107D
    L10A5           := $10A5
    L10B2           := $10B2
    L10BB           := $10BB
    L10D3           := $10D3
    L10ED           := $10ED
    jsr     pushax
    jsr     decsp2
    ldy     #$00
    tya
    sta     ($00),y
    iny
    sta     ($00),y
L1012:  ldy     #$01
    lda     ($00),y
    tax
    dey
    lda     ($00),y
    ldy     #$04
    cmp     ($00),y
    txa
    iny
    sbc     ($00),y
    bvc     L1026
    eor     #$80
L1026:  bpl     L1075
    ldy     #$09
    jsr     pushwysp
    ldy     #$03
    lda     ($00),y
    sta     $10
    dey
    lda     ($00),y
    asl     a
    rol     $10
    ldx     $10
    jsr     tosaddax
    sta     $08
    stx     $09
    ldy     #$01
    lda     ($08),y
    tax
    dey
    lda     ($08),y
    ldy     #$02
    cmp     ($00),y
    bne     L1061
    txa
    iny
    cmp     ($00),y
    bne     L1061
    ldy     #$01
    lda     ($00),y
    tax
    dey
    lda     ($00),y
    jmp     incsp8
L1061:  ldy     #$01
    lda     ($00),y
    tax
    dey
    lda     ($00),y
    clc
    adc     #$01
    bcc     L106F
    inx
L106F:  jsr     stax0sp
    jmp     L1012
L1075:  ldx     #$FF
    txa
    jmp     incsp8
```

## fnptr

| kompilator | bajty funkcji | instrukcje | wołane procedury zewnętrzne |
| --- | ---: | ---: | --- |
| nasz | 165 | 83 | - |
| cc65 | 115 | 63 | incsp2, jmpvec, negax, pushax, pushwysp |

stosunek nasz / cc65: 1.43

- cc65: sama funkcja 115 B; po zlinkowaniu z runtime cc65 240 B (w tym 4 B startera)

### nasz

```asm
    L1077           := $1077
    lda     $14
    clc
    adc     $14
    sta     $1E
    lda     $15
    adc     $15
    sta     $1F
    lda     $1E
    sta     $1A
    lda     $1F
    sta     $1B
    rts
    lda     #$00
    sec
    sbc     $14
    sta     $20
    lda     #$00
    sbc     $15
    sta     $21
    lda     $20
    sta     $1A
    lda     $21
    sta     $1B
    rts
L10A6:  lda     $14
    sta     $22
    lda     $15
    sta     $23
    lda     $16
    sta     $14
    lda     $17
    sta     $15
    lda     $22
    sta     $8042
    lda     $23
    sta     $8043
    jsr     L1077
    lda     $1A
    sta     $24
    lda     $1B
    sta     $25
    lda     $24
    sta     $1A
    lda     $25
    sta     $1B
    rts
    lda     $14
    cmp     #$00
    bne     L10DD
    jmp     L10FB
L10DD:  lda     #$7A
    sta     $14
    lda     #$10
    sta     $15
    jsr     L10A6
    lda     $1A
    sta     $28
    lda     $1B
    sta     $29
    lda     $28
    sta     $26
    lda     $29
    sta     $27
    jmp     L1116
L10FB:  lda     #$90
    sta     $14
    lda     #$10
    sta     $15
    jsr     L10A6
    lda     $1A
    sta     $28
    lda     $1B
    sta     $29
    lda     $28
    sta     $26
    lda     $29
    sta     $27
L1116:  lda     $26
    sta     $1A
    lda     $27
    sta     $1B
    rts
```

### cc65

```asm
    L108D           := $108D
    L109B           := $109B
    L10A0           := $10A0
    L10A9           := $10A9
    L10BB           := $10BB
    L10D3           := $10D3
    L10ED           := $10ED
    jsr     pushax
    ldy     #$00
    lda     ($00),y
    clc
    adc     ($00),y
    pha
    iny
    lda     ($00),y
    adc     ($00),y
    tax
    pla
    jmp     incsp2
    jsr     pushax
    ldy     #$01
    lda     ($00),y
    tax
    dey
    lda     ($00),y
    jsr     negax
    jmp     incsp2
L102A:  jsr     pushax
    ldy     #$05
    jsr     pushwysp
    ldy     #$03
    lda     ($00),y
    tax
    dey
    lda     ($00),y
    pha
    ldy     #$00
    lda     ($00),y
    sta     $10EE
    iny
    lda     ($00),y
    sta     $10EF
    pla
    jsr     jmpvec
    jsr     incsp2
    jmp     incsp4
    jsr     pushax
    ldy     #$02
    lda     ($00),y
    beq     L1062
    lda     #$04
    ldx     #$10
    jmp     L1066
L1062:  lda     #$19
    ldx     #$10
L1066:  jsr     pushax
    ldy     #$03
    lda     ($00),y
    tax
    dey
    lda     ($00),y
    jsr     L102A
    jmp     incsp3
```

## max3

| kompilator | bajty funkcji | instrukcje | wołane procedury zewnętrzne |
| --- | ---: | ---: | --- |
| nasz | 83 | 42 | - |
| cc65 | 90 | 52 | decsp2, pushax, pushwysp, stax0sp, tosicmp |

stosunek nasz / cc65: 0.92

- cc65: sama funkcja 90 B; po zlinkowaniu z runtime cc65 237 B (w tym 4 B startera)

### nasz

```asm
    lda     $14
    sta     $1E
    lda     $15
    sta     $1F
    eor     #$80
    sta     $1C
    lda     $17
    eor     #$80
    sta     $1D
    lda     $14
    sec
    sbc     $16
    lda     $1C
    sbc     $1D
    bcs     L10A2
    lda     $16
    sta     $1E
    lda     $17
    sta     $1F
    jmp     L10A2
L10A2:  lda     $1F
    eor     #$80
    sta     $1C
    lda     $19
    eor     #$80
    sta     $1D
    lda     $1E
    sec
    sbc     $18
    lda     $1C
    sbc     $1D
    bcs     L10C4
    lda     $18
    sta     $1E
    lda     $19
    sta     $1F
    jmp     L10C4
L10C4:  lda     $1E
    sta     $1A
    lda     $1F
    sta     $1B
    rts
```

### cc65

```asm
    L106C           := $106C
    L107B           := $107B
    L10A7           := $10A7
    L10B0           := $10B0
    L10C8           := $10C8
    L10E2           := $10E2
    jsr     pushax
    jsr     decsp2
    ldy     #$07
    lda     ($00),y
    tax
    dey
    lda     ($00),y
    jsr     stax0sp
    ldy     #$07
    jsr     pushwysp
    ldy     #$03
    lda     ($00),y
    tax
    dey
    lda     ($00),y
    jsr     tosicmp
    bmi     L1034
    beq     L1034
    ldy     #$05
    lda     ($00),y
    tax
    dey
    lda     ($00),y
    jsr     stax0sp
L1034:  ldy     #$05
    jsr     pushwysp
    ldy     #$03
    lda     ($00),y
    tax
    dey
    lda     ($00),y
    jsr     tosicmp
    bmi     L1053
    beq     L1053
    ldy     #$03
    lda     ($00),y
    tax
    dey
    lda     ($00),y
    jsr     stax0sp
L1053:  ldy     #$01
    lda     ($00),y
    tax
    dey
    lda     ($00),y
    jmp     incsp8
```

## mul16

| kompilator | bajty funkcji | instrukcje | wołane procedury zewnętrzne |
| --- | ---: | ---: | --- |
| nasz | 20 | 11 | - |
| cc65 | 22 | 14 | pushax, pushwysp, tosmulax |

stosunek nasz / cc65: 0.91

- cc65: sama funkcja 22 B; po zlinkowaniu z runtime cc65 262 B (w tym 4 B startera)

### nasz

```asm
    L1095           := $1095
    jsr     L1095
    lda     $1A
    sta     $1E
    lda     $1B
    sta     $1F
    lda     $1E
    sta     $1A
    lda     $1F
    sta     $1B
    rts
```

### cc65

```asm
    L103E           := $103E
    L1043           := $1043
    L10D4           := $10D4
    L10EC           := $10EC
    jsr     pushax
    ldy     #$05
    jsr     pushwysp
    ldy     #$03
    lda     ($00),y
    tax
    dey
    lda     ($00),y
    jsr     tosmulax
    jmp     incsp4
```

## shift

| kompilator | bajty funkcji | instrukcje | wołane procedury zewnętrzne |
| --- | ---: | ---: | --- |
| nasz | 20 | 11 | - |
| cc65 | 28 | 17 | pusha, shlaxy |

stosunek nasz / cc65: 0.71

- cc65: sama funkcja 28 B; po zlinkowaniu z runtime cc65 146 B (w tym 4 B startera)

### nasz

```asm
    L1095           := $1095
    jsr     L1095
    lda     $1A
    sta     $1E
    lda     $1B
    sta     $1F
    lda     $1E
    sta     $1A
    lda     $1F
    sta     $1B
    rts
```

### cc65

```asm
    L1044           := $1044
    L104D           := $104D
    L1066           := $1066
    jsr     pusha
    ldy     #$02
    lda     ($00),y
    sta     $09
    dey
    lda     ($00),y
    sta     $08
    dey
    lda     ($00),y
    tay
    lda     $08
    ldx     $09
    jsr     shlaxy
    jmp     incsp3
```

## str_len

| kompilator | bajty funkcji | instrukcje | wołane procedury zewnętrzne |
| --- | ---: | ---: | --- |
| nasz | 43 | 21 | - |
| cc65 | 78 | 48 | decsp2, pushax, stax0sp, staxysp |

stosunek nasz / cc65: 0.55

- cc65: sama funkcja 78 B; po zlinkowaniu z runtime cc65 151 B (w tym 4 B startera)

### nasz

```asm
    lda     #$00
    sta     $1E
    sta     $1F
L1080:  ldy     #$00
    lda     ($14),y
    sta     $20
    cmp     #$00
    bne     L108D
    jmp     L109C
L108D:  inc     $1E
    bne     L1093
    inc     $1F
L1093:  inc     $14
    bne     L1099
    inc     $15
L1099:  jmp     L1080
L109C:  lda     $1E
    sta     $1A
    lda     $1F
    sta     $1B
    rts
```

### cc65

```asm
    L1060           := $1060
    L106D           := $106D
    L1076           := $1076
    L108C           := $108C
    L108E           := $108E
    jsr     pushax
    jsr     decsp2
    ldy     #$00
    tya
    sta     ($00),y
    iny
    sta     ($00),y
    jmp     L1037
L1015:  ldy     #$01
    lda     ($00),y
    tax
    dey
    lda     ($00),y
    clc
    adc     #$01
    bcc     L1023
    inx
L1023:  jsr     stax0sp
    ldy     #$03
    lda     ($00),y
    tax
    dey
    lda     ($00),y
    clc
    adc     #$01
    bcc     L1034
    inx
L1034:  jsr     staxysp
L1037:  ldy     #$03
    lda     ($00),y
    sta     $09
    dey
    lda     ($00),y
    sta     $08
    ldy     #$00
    lda     ($08),y
    bne     L1015
    iny
    lda     ($00),y
    tax
    dey
    lda     ($00),y
    jmp     incsp4
```

## structs

| kompilator | bajty funkcji | instrukcje | wołane procedury zewnętrzne |
| --- | ---: | ---: | --- |
| nasz | 167 | 87 | - |
| cc65 | 200 | 114 | pushax, pushwysp, staspidx, staxspidx, tosadda0, tosaddax, tossubax |

stosunek nasz / cc65: 0.83

- cc65: sama funkcja 200 B; po zlinkowaniu z runtime cc65 397 B (w tym 4 B startera)

### nasz

```asm
    ldy     #$00
    lda     ($16),y
    sta     $1E
    ldy     #$01
    lda     ($16),y
    sta     $1F
    ldy     #$00
    lda     ($14),y
    sta     $20
    ldy     #$01
    lda     ($14),y
    sta     $21
    lda     $1E
    sec
    sbc     $20
    sta     $1E
    lda     $1F
    sbc     $21
    sta     $1F
    ldy     #$02
    lda     ($16),y
    sta     $20
    ldy     #$03
    lda     ($16),y
    sta     $21
    ldy     #$02
    lda     ($14),y
    sta     $22
    ldy     #$03
    lda     ($14),y
    sta     $23
    lda     $20
    sec
    sbc     $22
    sta     $20
    lda     $21
    sbc     $23
    sta     $21
    lda     $1E
    clc
    adc     $20
    sta     $1E
    lda     $1F
    adc     $21
    sta     $1F
    ldy     #$04
    lda     ($16),y
    sta     $20
    lda     $1E
    clc
    adc     $20
    sta     $1E
    lda     $1F
    adc     #$00
    sta     $1F
    lda     $1E
    sta     $1A
    lda     $1F
    sta     $1B
    rts
    ldy     #$00
    lda     ($14),y
    sta     $24
    ldy     #$01
    lda     ($14),y
    sta     $25
    lda     $24
    clc
    adc     $16
    sta     $24
    lda     $25
    adc     $17
    sta     $25
    lda     $24
    ldy     #$00
    sta     ($14),y
    lda     $25
    ldy     #$01
    sta     ($14),y
    ldy     #$04
    lda     ($14),y
    sta     $26
    inc     $26
    lda     $26
    ldy     #$04
    sta     ($14),y
    rts
```

### cc65

```asm
    L10CC           := $10CC
    L10CE           := $10CE
    L110C           := $110C
    L1115           := $1115
    L112D           := $112D
    L1147           := $1147
    L115D           := $115D
    L117A           := $117A
    jsr     pushax
    ldy     #$01
    lda     ($00),y
    sta     $09
    dey
    lda     ($00),y
    sta     $08
    iny
    lda     ($08),y
    tax
    dey
    lda     ($08),y
    jsr     pushax
    ldy     #$05
    lda     ($00),y
    sta     $09
    dey
    lda     ($00),y
    sta     $08
    ldy     #$01
    lda     ($08),y
    tax
    dey
    lda     ($08),y
    jsr     tossubax
    jsr     pushax
    ldy     #$03
    lda     ($00),y
    sta     $09
    dey
    lda     ($00),y
    sta     $08
    iny
    lda     ($08),y
    tax
    dey
    lda     ($08),y
    jsr     pushax
    ldy     #$07
    lda     ($00),y
    sta     $09
    dey
    lda     ($00),y
    sta     $08
    ldy     #$03
    lda     ($08),y
    tax
    dey
    lda     ($08),y
    jsr     tossubax
    jsr     tosaddax
    jsr     pushax
    ldy     #$03
    lda     ($00),y
    sta     $09
    dey
    lda     ($00),y
    sta     $08
    ldy     #$04
    lda     ($08),y
    jsr     tosadda0
    jmp     incsp4
    jsr     pushax
    ldy     #$05
    jsr     pushwysp
    ldy     #$05
    lda     ($00),y
    sta     $09
    dey
    lda     ($00),y
    sta     $08
    ldy     #$01
    lda     ($08),y
    tax
    dey
    lda     ($08),y
    sta     $08
    stx     $09
    ldy     #$03
    lda     ($00),y
    tax
    dey
    lda     ($00),y
    clc
    adc     $08
    pha
    txa
    adc     $09
    tax
    pla
    ldy     #$00
    jsr     staxspidx
    ldy     #$03
    lda     ($00),y
    tax
    dey
    lda     ($00),y
    jsr     pushax
    sta     $08
    stx     $09
    ldy     #$04
    lda     ($08),y
    clc
    adc     #$01
    jsr     staspidx
    jmp     incsp4
```

## sum_bytes

| kompilator | bajty funkcji | instrukcje | wołane procedury zewnętrzne |
| --- | ---: | ---: | --- |
| nasz | 54 | 27 | - |
| cc65 | 81 | 49 | decsp2, pusha, pushw0sp, stax0sp, staxysp, tosadda0 |

stosunek nasz / cc65: 0.67

- cc65: sama funkcja 81 B; po zlinkowaniu z runtime cc65 206 B (w tym 4 B startera)

### nasz

```asm
    lda     #$00
    sta     $1E
    sta     $1F
L1080:  lda     $16
    cmp     #$00
    bne     L1089
    jmp     L10A7
L1089:  ldy     #$00
    lda     ($14),y
    sta     $20
    lda     $1E
    clc
    adc     $20
    sta     $1E
    lda     $1F
    adc     #$00
    sta     $1F
    inc     $14
    bne     L10A2
    inc     $15
L10A2:  dec     $16
    jmp     L1080
L10A7:  lda     $1E
    sta     $1A
    lda     $1F
    sta     $1B
    rts
```

### cc65

```asm
    L1055           := $1055
    L107F           := $107F
    L108C           := $108C
    L1095           := $1095
    L10A7           := $10A7
    L10C3           := $10C3
    L10C5           := $10C5
    jsr     pusha
    jsr     decsp2
    ldy     #$00
    tya
    sta     ($00),y
    iny
    jmp     L1043
L1013:  jsr     pushw0sp
    ldy     #$06
    lda     ($00),y
    sta     $09
    dey
    lda     ($00),y
    sta     $08
    ldx     #$00
    lda     ($08,x)
    jsr     tosadda0
    jsr     stax0sp
    ldy     #$04
    lda     ($00),y
    tax
    dey
    lda     ($00),y
    clc
    adc     #$01
    bcc     L1039
    inx
L1039:  jsr     staxysp
    ldy     #$02
    lda     ($00),y
    sec
    sbc     #$01
L1043:  sta     ($00),y
    ldy     #$02
    lda     ($00),y
    bne     L1013
    dey
    lda     ($00),y
    tax
    dey
    lda     ($00),y
    jmp     incsp5
```

## sw

| kompilator | bajty funkcji | instrukcje | wołane procedury zewnętrzne |
| --- | ---: | ---: | --- |
| nasz | 120 | 55 | - |
| cc65 | 57 | 27 | pusha |

stosunek nasz / cc65: 2.11

- cc65: sama funkcja 57 B; po zlinkowaniu z runtime cc65 90 B (w tym 4 B startera)

### nasz

```asm
    lda     $14
    sta     $1E
    lda     #$00
    sta     $1F
    lda     $1E
    ora     $1F
    beq     L10C7
    lda     $1E
    cmp     #$01
    bne     L1097
    lda     $1F
    cmp     #$00
    bne     L1097
    jmp     L10CE
L1097:  lda     $1E
    cmp     #$02
    bne     L10A6
    lda     $1F
    cmp     #$00
    bne     L10A6
    jmp     L10D5
L10A6:  lda     $1E
    cmp     #$03
    bne     L10B5
    lda     $1F
    cmp     #$00
    bne     L10B5
    jmp     L10DC
L10B5:  lda     $1E
    cmp     #$04
    bne     L10C4
    lda     $1F
    cmp     #$00
    bne     L10C4
    jmp     L10E3
L10C4:  jmp     L10EA
L10C7:  lda     #$0A
    sta     $1A
    jmp     L10F1
L10CE:  lda     #$14
    sta     $1A
    jmp     L10F1
L10D5:  lda     #$23
    sta     $1A
    jmp     L10F1
L10DC:  lda     #$29
    sta     $1A
    jmp     L10F1
L10E3:  lda     #$39
    sta     $1A
    jmp     L10F1
L10EA:  lda     #$00
    sta     $1A
    jmp     L10F1
L10F1:  rts
```

### cc65

```asm
    L103D           := $103D
    L1048           := $1048
    jsr     pusha
    ldx     #$00
    lda     ($00,x)
    beq     L1020
    cmp     #$01
    beq     L1025
    cmp     #$02
    beq     L102A
    cmp     #$03
    beq     L102F
    cmp     #$04
    beq     L1034
    jmp     L1039
L1020:  lda     #$0A
    jmp     incsp1
L1025:  lda     #$14
    jmp     incsp1
L102A:  lda     #$23
    jmp     incsp1
L102F:  lda     #$29
    jmp     incsp1
L1034:  lda     #$39
    jmp     incsp1
L1039:  txa
    jmp     incsp1
```
