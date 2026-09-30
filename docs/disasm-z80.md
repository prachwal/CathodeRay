# Deasemblacja: nasz kompilator vs SDCC (z80)

## add32

| kompilator | bajty funkcji | instrukcje | wołane procedury zewnętrzne |
| --- | ---: | ---: | --- |
| nasz | 170 | 63 | - |
| sdcc | 33 | 19 | - |

stosunek nasz / sdcc: 5.15

- sdcc: adresy wywołań zewnętrznych w .rel są niezrelokowane (widoczne jako 0000)

### nasz

```asm
    ld a,(07001h)
    ld (07011h),a
    ld a,(07002h)
    ld (07012h),a
    ld a,(07003h)
    ld (07013h),a
    ld a,(07004h)
    ld (07014h),a
    ld a,(07005h)
    ld (07015h),a
    ld a,(07006h)
    ld (07016h),a
    ld a,(07007h)
    ld (07017h),a
    ld a,(07008h)
    ld (07018h),a
    ld a,(07011h)
    ld hl,07015h
    add a,(hl)
    ld (0701dh),a
    ld a,(07012h)
    ld hl,07016h
    adc a,(hl)
    ld (0701eh),a
    ld a,000h
    ld (0701fh),a
    ld a,(0701dh)
    ld hl,07011h
    sub (hl)
    ld a,(0701eh)
    ld hl,07012h
    sbc a,(hl)
    jp nc,0109ah
    ld a,001h
    ld (0701fh),a
    ld a,(07013h)
    ld hl,07017h
    add a,(hl)
    ld (0701bh),a
    ld a,(07014h)
    ld hl,07018h
    adc a,(hl)
    ld (0701ch),a
    ld a,(0701bh)
    ld hl,0701fh
    add a,(hl)
    ld (0701bh),a
    ld a,(0701ch)
    adc a,000h
    ld (0701ch),a
    ld a,(0701dh)
    ld (07019h),a
    ld a,(0701eh)
    ld (0701ah),a
    ld a,(0701bh)
    ld (08040h),a
    ld a,(0701ch)
    ld (08041h),a
    ld a,(07019h)
    ld (0700dh),a
    ld a,(0701ah)
    ld (0700eh),a
    ret
```

### sdcc

```asm
    push ix
    ld ix,00000h
    add ix,sp
    ld c,l
    ld b,h
    ld a,e
    add a,(ix+004h)
    ld e,a
    ld a,d
    adc a,(ix+005h)
    ld d,a
    ld a,c
    adc a,(ix+006h)
    ld l,a
    ld a,b
    adc a,(ix+007h)
    ld h,a
    pop ix
    ret
```

## bubble

| kompilator | bajty funkcji | instrukcje | wołane procedury zewnętrzne |
| --- | ---: | ---: | --- |
| nasz | 586 | 236 | - |
| sdcc | 276 | 111 | - |

stosunek nasz / sdcc: 2.12

- sdcc: adresy wywołań zewnętrznych w .rel są niezrelokowane (widoczne jako 0000)

### nasz

```asm
    ld a,(07001h)
    ld (07011h),a
    ld a,(07002h)
    ld (07012h),a
    ld a,(07003h)
    ld (07013h),a
    ld a,(07004h)
    ld (07014h),a
    ld a,000h
    ld (07015h),a
    ld (07016h),a
    ld a,(07013h)
    sub 001h
    ld (0701dh),a
    ld a,(07014h)
    sbc a,000h
    ld (0701eh),a
    ld a,(07016h)
    xor 080h
    ld (0700fh),a
    ld a,(0701eh)
    xor 080h
    ld (07010h),a
    ld a,(07015h)
    ld hl,0701dh
    sub (hl)
    ld a,(0700fh)
    ld hl,07010h
    sbc a,(hl)
    jp nc,01284h
    ld a,000h
    ld (07017h),a
    ld (07018h),a
    ld a,(07013h)
    sub 001h
    ld (0701dh),a
    ld a,(07014h)
    sbc a,000h
    ld (0701eh),a
    ld a,(0701dh)
    ld hl,07015h
    sub (hl)
    ld (0701dh),a
    ld a,(0701eh)
    ld hl,07016h
    sbc a,(hl)
    ld (0701eh),a
    ld a,(07018h)
    xor 080h
    ld (0700fh),a
    ld a,(0701eh)
    xor 080h
    ld (07010h),a
    ld a,(07017h)
    ld hl,0701dh
    sub (hl)
    ld a,(0700fh)
    ld hl,07010h
    sbc a,(hl)
    jp nc,01279h
    ld a,(07017h)
    ld (0701dh),a
    ld a,(07018h)
    ld (0701eh),a
    ld a,(0701dh)
    sla a
    ld (0701dh),a
    ld a,(0701eh)
    rl a
    ld (0701eh),a
    ld a,(07011h)
    ld hl,0701dh
    add a,(hl)
    ld (0701bh),a
    ld a,(07012h)
    ld hl,0701eh
    adc a,(hl)
    ld (0701ch),a
    ld hl,(0701bh)
    ld a,(hl)
    ld (0701bh),a
    inc hl
    ld a,(hl)
    ld (0701ch),a
    ld a,(07017h)
    add a,001h
    ld (0701fh),a
    ld a,(07018h)
    adc a,000h
    ld (07020h),a
    ld a,(0701fh)
    sla a
    ld (0701fh),a
    ld a,(07020h)
    rl a
    ld (07020h),a
    ld a,(07011h)
    ld hl,0701fh
    add a,(hl)
    ld (0701dh),a
    ld a,(07012h)
    ld hl,07020h
    adc a,(hl)
    ld (0701eh),a
    ld hl,(0701dh)
    ld a,(hl)
    ld (0701dh),a
    inc hl
    ld a,(hl)
    ld (0701eh),a
    xor 080h
    ld (0700fh),a
    ld a,(0701ch)
    xor 080h
    ld (07010h),a
    ld a,(0701dh)
    ld hl,0701bh
    sub (hl)
    ld a,(0700fh)
    ld hl,07010h
    sbc a,(hl)
    jp nc,0126eh
    ld a,(07017h)
    ld (0701dh),a
    ld a,(07018h)
    ld (0701eh),a
    ld a,(0701dh)
    sla a
    ld (0701dh),a
    ld a,(0701eh)
    rl a
    ld (0701eh),a
    ld a,(07011h)
    ld hl,0701dh
    add a,(hl)
    ld (0701bh),a
    ld a,(07012h)
    ld hl,0701eh
    adc a,(hl)
    ld (0701ch),a
    ld hl,(0701bh)
    ld a,(hl)
    ld (07019h),a
    inc hl
    ld a,(hl)
    ld (0701ah),a
    ld a,(07017h)
    add a,001h
    ld (0701dh),a
    ld a,(07018h)
    adc a,000h
    ld (0701eh),a
    ld a,(0701dh)
    sla a
    ld (0701dh),a
    ld a,(0701eh)
    rl a
    ld (0701eh),a
    ld a,(07011h)
    ld hl,0701dh
    add a,(hl)
    ld (0701bh),a
    ld a,(07012h)
    ld hl,0701eh
    adc a,(hl)
    ld (0701ch),a
    ld hl,(0701bh)
    ld a,(hl)
    ld (0701bh),a
    inc hl
    ld a,(hl)
    ld (0701ch),a
    ld a,(07017h)
    ld (0701fh),a
    ld a,(07018h)
    ld (07020h),a
    ld a,(0701fh)
    sla a
    ld (0701fh),a
    ld a,(07020h)
    rl a
    ld (07020h),a
    ld a,(07011h)
    ld hl,0701fh
    add a,(hl)
    ld (0701dh),a
    ld a,(07012h)
    ld hl,07020h
    adc a,(hl)
    ld (0701eh),a
    ld hl,(0701dh)
    ld a,(0701bh)
    ld (hl),a
    ld a,(0701ch)
    inc hl
    ld (hl),a
    ld a,(07017h)
    add a,001h
    ld (0701fh),a
    ld a,(07018h)
    adc a,000h
    ld (07020h),a
    ld a,(0701fh)
    sla a
    ld (0701fh),a
    ld a,(07020h)
    rl a
    ld (07020h),a
    ld a,(07011h)
    ld hl,0701fh
    add a,(hl)
    ld (0701dh),a
    ld a,(07012h)
    ld hl,07020h
    adc a,(hl)
    ld (0701eh),a
    ld hl,(0701dh)
    ld a,(07019h)
    ld (hl),a
    ld a,(0701ah)
    inc hl
    ld (hl),a
    jp 0126eh
    ld hl,07017h
    inc (hl)
    jr nz,$+4
    inc hl
    inc (hl)
    jp 01094h
    ld hl,07015h
    inc (hl)
    jr nz,$+4
    inc hl
    inc (hl)
    jp 0105bh
    ret
```

### sdcc

```asm
    push ix
    ld ix,00000h
    add ix,sp
    ld iy,0ffeeh
    add iy,sp
    ld sp,iy
    ld (ix-004h),l
    ld (ix-003h),h
    ld a,e
    add a,0ffh
    ld (ix-012h),a
    ld a,d
    adc a,0ffh
    ld (ix-011h),a
    xor a
    ld (ix-002h),a
    ld (ix-001h),a
    ld a,(ix-002h)
    sub (ix-012h)
    ld a,(ix-001h)
    sbc a,(ix-011h)
    jp po,0003ah
    xor 080h
    jp p,0010fh
    ld a,(ix-012h)
    sub (ix-002h)
    ld (ix-010h),a
    ld a,(ix-011h)
    sbc a,(ix-001h)
    ld (ix-00fh),a
    xor a
    ld (ix-006h),a
    ld (ix-005h),a
    ld a,(ix-006h)
    sub (ix-010h)
    ld a,(ix-005h)
    sbc a,(ix-00fh)
    jp po,00067h
    xor 080h
    jp p,00103h
    ld l,(ix-006h)
    ld h,(ix-005h)
    add hl,hl
    ld a,l
    add a,(ix-004h)
    ld (ix-00eh),a
    ld a,h
    adc a,(ix-003h)
    ld (ix-00dh),a
    ld l,(ix-00eh)
    ld h,(ix-00dh)
    ld a,(hl)
    ld (ix-00ch),a
    inc hl
    ld a,(hl)
    ld (ix-00bh),a
    ld a,(ix-006h)
    add a,001h
    ld (ix-00ah),a
    ld a,(ix-005h)
    adc a,000h
    ld (ix-009h),a
    ld l,(ix-00ah)
    ld h,(ix-009h)
    add hl,hl
    ld a,(ix-004h)
    add a,l
    ld (ix-008h),a
    ld a,(ix-003h)
    adc a,h
    ld (ix-007h),a
    ld l,(ix-008h)
    ld h,(ix-007h)
    ld a,(hl)
    ld (ix-006h),a
    inc hl
    ld a,(hl)
    ld (ix-005h),a
    ld a,(ix-006h)
    sub (ix-00ch)
    ld a,(ix-005h)
    sbc a,(ix-00bh)
    jp po,000d3h
    xor 080h
    jp p,000f4h
    ld c,(ix-00ch)
    ld b,(ix-00bh)
    ld l,(ix-00eh)
    ld h,(ix-00dh)
    ld a,(ix-006h)
    ld (hl),a
    inc hl
    ld a,(ix-005h)
    ld (hl),a
    ld l,(ix-008h)
    ld h,(ix-007h)
    ld (hl),c
    inc hl
    ld (hl),b
    ld a,(ix-00ah)
    ld (ix-006h),a
    ld a,(ix-009h)
    ld (ix-005h),a
    jp 00056h
    inc (ix-002h)
    jp nz,00029h
    inc (ix-001h)
    jp 00029h
    ld sp,ix
    pop ix
    ret
```

## copy

| kompilator | bajty funkcji | instrukcje | wołane procedury zewnętrzne |
| --- | ---: | ---: | --- |
| nasz | 90 | 41 | - |
| sdcc | 30 | 18 | - |

stosunek nasz / sdcc: 3.00

- sdcc: adresy wywołań zewnętrznych w .rel są niezrelokowane (widoczne jako 0000)

### nasz

```asm
    ld a,(07001h)
    ld (07011h),a
    ld a,(07002h)
    ld (07012h),a
    ld a,(07003h)
    ld (07013h),a
    ld a,(07004h)
    ld (07014h),a
    ld a,(07005h)
    ld (07015h),a
    ld a,(07006h)
    ld (07016h),a
    ld a,(07015h)
    ld hl,07016h
    or (hl)
    jp z,01094h
    ld hl,(07013h)
    ld a,(hl)
    ld (07017h),a
    ld hl,(07011h)
    ld a,(07017h)
    ld (hl),a
    ld hl,07011h
    inc (hl)
    jr nz,$+4
    inc hl
    inc (hl)
    ld hl,07013h
    inc (hl)
    jr nz,$+4
    inc hl
    inc (hl)
    ld hl,07015h
    ld a,(hl)
    dec (hl)
    or a
    jr nz,$+4
    inc hl
    dec (hl)
    jp 0105fh
    ret
```

### sdcc

```asm
    push ix
    ld ix,00000h
    add ix,sp
    ld c,(ix+004h)
    ld b,(ix+005h)
    ld a,b
    or c
    jr z,$+9
    ld a,(de)
    ld (hl),a
    inc hl
    inc de
    dec bc
    jr $-9
    pop ix
    pop hl
    pop af
    jp (hl)
```

## div16

| kompilator | bajty funkcji | instrukcje | wołane procedury zewnętrzne |
| --- | ---: | ---: | --- |
| nasz | 135 | 47 | - |
| sdcc | 25 | 19 | divsint, modsint |

stosunek nasz / sdcc: 5.40

- sdcc: adresy wywołań zewnętrznych w .rel są niezrelokowane (widoczne jako 0000)

### nasz

```asm
    ld a,(07001h)
    ld (07011h),a
    ld a,(07002h)
    ld (07012h),a
    ld a,(07003h)
    ld (07013h),a
    ld a,(07004h)
    ld (07014h),a
    ld a,(07011h)
    ld (07001h),a
    ld a,(07012h)
    ld (07002h),a
    ld a,(07013h)
    ld (07003h),a
    ld a,(07014h)
    ld (07004h),a
    call 010cbh
    ld a,(0700dh)
    ld (07015h),a
    ld a,(0700eh)
    ld (07016h),a
    ld a,(07011h)
    ld (07001h),a
    ld a,(07012h)
    ld (07002h),a
    ld a,(07013h)
    ld (07003h),a
    ld a,(07014h)
    ld (07004h),a
    call 011cfh
    ld a,(0700dh)
    ld (07017h),a
    ld a,(0700eh)
    ld (07018h),a
    ld a,(07015h)
    ld hl,07017h
    add a,(hl)
    ld (07015h),a
    ld a,(07016h)
    ld hl,07018h
    adc a,(hl)
    ld (07016h),a
    ld a,(07015h)
    ld (0700dh),a
    ld a,(07016h)
    ld (0700eh),a
    ret
```

### sdcc

```asm
    ld c,e
    ld b,d
    push hl
    push bc
    ld e,c
    ld d,b
    call 00000h
    push de
    pop iy
    pop bc
    pop hl
    push iy
    ld e,c
    ld d,b
    call 00000h
    pop hl
    add hl,de
    ex de,hl
    ret
```

## fib

| kompilator | bajty funkcji | instrukcje | wołane procedury zewnętrzne |
| --- | ---: | ---: | --- |
| nasz | 212 | 83 | - |
| sdcc | 31 | 25 | - |

stosunek nasz / sdcc: 6.84

- sdcc: adresy wywołań zewnętrznych w .rel są niezrelokowane (widoczne jako 0000)

### nasz

```asm
    ld a,(07011h)
    push af
    ld a,(07012h)
    push af
    ld a,(07013h)
    push af
    ld a,(07014h)
    push af
    ld a,(07015h)
    push af
    ld a,(07016h)
    push af
    ld a,(07001h)
    ld (07011h),a
    ld a,(07002h)
    ld (07012h),a
    xor 080h
    ld (0700fh),a
    ld a,(07011h)
    sub 002h
    ld a,(0700fh)
    sbc a,080h
    jp nc,01080h
    ld a,(07011h)
    ld (0700dh),a
    ld a,(07012h)
    ld (0700eh),a
    jp 010f6h
    ld a,(07011h)
    sub 001h
    ld (07015h),a
    ld a,(07012h)
    sbc a,000h
    ld (07016h),a
    ld a,(07015h)
    ld (07001h),a
    ld a,(07016h)
    ld (07002h),a
    call 0103bh
    ld a,(0700dh)
    ld (07013h),a
    ld a,(0700eh)
    ld (07014h),a
    ld a,(07011h)
    sub 002h
    ld (07017h),a
    ld a,(07012h)
    sbc a,000h
    ld (07018h),a
    ld a,(07017h)
    ld (07001h),a
    ld a,(07018h)
    ld (07002h),a
    call 0103bh
    ld a,(0700dh)
    ld (07015h),a
    ld a,(0700eh)
    ld (07016h),a
    ld a,(07013h)
    ld hl,07015h
    add a,(hl)
    ld (07013h),a
    ld a,(07014h)
    ld hl,07016h
    adc a,(hl)
    ld (07014h),a
    ld a,(07013h)
    ld (0700dh),a
    ld a,(07014h)
    ld (0700eh),a
    pop af
    ld (07016h),a
    pop af
    ld (07015h),a
    pop af
    ld (07014h),a
    pop af
    ld (07013h),a
    pop af
    ld (07012h),a
    pop af
    ld (07011h),a
    ret
```

### sdcc

```asm
    ex de,hl
    ld a,e
    sub 002h
    ld a,d
    rla
    ccf
    rra
    sbc a,080h
    ret c
    ld l,e
    ld h,d
    dec hl
    push de
    call 00000h
    ex de,hl
    pop de
    dec de
    dec de
    ex de,hl
    push de
    call 00000h
    pop hl
    add hl,de
    ex de,hl
    ret
```

## find

| kompilator | bajty funkcji | instrukcje | wołane procedury zewnętrzne |
| --- | ---: | ---: | --- |
| nasz | 192 | 75 | - |
| sdcc | 106 | 49 | - |

stosunek nasz / sdcc: 1.81

- sdcc: adresy wywołań zewnętrznych w .rel są niezrelokowane (widoczne jako 0000)

### nasz

```asm
    ld a,(07001h)
    ld (07011h),a
    ld a,(07002h)
    ld (07012h),a
    ld a,(07003h)
    ld (07013h),a
    ld a,(07004h)
    ld (07014h),a
    ld a,(07005h)
    ld (07015h),a
    ld a,(07006h)
    ld (07016h),a
    ld a,000h
    ld (07017h),a
    ld (07018h),a
    ld a,(07018h)
    xor 080h
    ld (0700fh),a
    ld a,(07014h)
    xor 080h
    ld (07010h),a
    ld a,(07017h)
    ld hl,07013h
    sub (hl)
    ld a,(0700fh)
    ld hl,07010h
    sbc a,(hl)
    jp nc,010f2h
    ld a,(07017h)
    ld (0701bh),a
    ld a,(07018h)
    ld (0701ch),a
    ld a,(0701bh)
    sla a
    ld (0701bh),a
    ld a,(0701ch)
    rl a
    ld (0701ch),a
    ld a,(07011h)
    ld hl,0701bh
    add a,(hl)
    ld (07019h),a
    ld a,(07012h)
    ld hl,0701ch
    adc a,(hl)
    ld (0701ah),a
    ld hl,(07019h)
    ld a,(hl)
    ld (07019h),a
    inc hl
    ld a,(hl)
    ld (0701ah),a
    ld a,(07019h)
    ld hl,07015h
    cp (hl)
    jp nz,010e7h
    ld a,(0701ah)
    ld hl,07016h
    cp (hl)
    jp nz,010e7h
    ld a,(07017h)
    ld (0700dh),a
    ld a,(07018h)
    ld (0700eh),a
    jp 010fah
    ld hl,07017h
    inc (hl)
    jr nz,$+4
    inc hl
    inc (hl)
    jp 01067h
    ld a,0ffh
    ld (0700dh),a
    ld (0700eh),a
    ret
```

### sdcc

```asm
    push ix
    ld ix,00000h
    add ix,sp
    push af
    push af
    push af
    ld (ix-004h),l
    ld (ix-003h),h
    inc sp
    inc sp
    push de
    ld de,00000h
    xor a
    ld (ix-002h),a
    ld (ix-001h),a
    ld a,(ix-002h)
    sub (ix-006h)
    ld a,(ix-001h)
    sbc a,(ix-005h)
    jp po,0002fh
    xor 080h
    jp p,00060h
    ld l,(ix-002h)
    ld h,(ix-001h)
    add hl,hl
    ld c,(ix-004h)
    ld b,(ix-003h)
    add hl,bc
    ld c,(hl)
    inc hl
    ld b,(hl)
    ld l,(ix+004h)
    ld h,(ix+005h)
    cp a
    sbc hl,bc
    jr z,$+23
    jr $+2
    inc (ix-002h)
    jr nz,$+5
    inc (ix-001h)
    ld e,(ix-002h)
    ld d,(ix-001h)
    jr $-64
    ld de,0ffffh
    ld sp,ix
    pop ix
    pop hl
    pop af
    jp (hl)
```

## fnptr

| kompilator | bajty funkcji | instrukcje | wołane procedury zewnętrzne |
| --- | ---: | ---: | --- |
| nasz | 302 | 107 | - |
| sdcc | 33 | 22 | - |

stosunek nasz / sdcc: 9.15

- sdcc: adresy wywołań zewnętrznych w .rel są niezrelokowane (widoczne jako 0000)

### nasz

```asm
    ld a,(07001h)
    ld (07011h),a
    ld a,(07002h)
    ld (07012h),a
    ld a,(07011h)
    ld hl,07011h
    add a,(hl)
    ld (07013h),a
    ld a,(07012h)
    ld hl,07012h
    adc a,(hl)
    ld (07014h),a
    ld a,(07013h)
    ld (0700dh),a
    ld a,(07014h)
    ld (0700eh),a
    ret
    ld a,(07001h)
    ld (07015h),a
    ld a,(07002h)
    ld (07016h),a
    ld a,000h
    ld hl,07015h
    sub (hl)
    ld (07017h),a
    ld a,000h
    ld hl,07016h
    sbc a,(hl)
    ld (07018h),a
    ld a,(07017h)
    ld (0700dh),a
    ld a,(07018h)
    ld (0700eh),a
    ret
    ld a,(07001h)
    ld (07019h),a
    ld a,(07002h)
    ld (0701ah),a
    ld a,(07003h)
    ld (0701bh),a
    ld a,(07004h)
    ld (0701ch),a
    ld a,(0701bh)
    ld (07001h),a
    ld a,(0701ch)
    ld (07002h),a
    ld hl,(07019h)
    call 0103ah
    ld a,(0700dh)
    ld (0701dh),a
    ld a,(0700eh)
    ld (0701eh),a
    ld a,(0701dh)
    ld (0700dh),a
    ld a,(0701eh)
    ld (0700eh),a
    ret
    ld a,(07001h)
    ld (0701fh),a
    ld a,(07003h)
    ld (07020h),a
    ld a,(07004h)
    ld (07021h),a
    ld a,(0701fh)
    cp 000h
    jp nz,010f3h
    jp 01129h
    ld a,(08042h)
    ld (07001h),a
    ld a,(08043h)
    ld (07002h),a
    ld a,(07020h)
    ld (07003h),a
    ld a,(07021h)
    ld (07004h),a
    call 01093h
    ld a,(0700dh)
    ld (07024h),a
    ld a,(0700eh)
    ld (07025h),a
    ld a,(07024h)
    ld (07022h),a
    ld a,(07025h)
    ld (07023h),a
    jp 0115ch
    ld a,(08044h)
    ld (07001h),a
    ld a,(08045h)
    ld (07002h),a
    ld a,(07020h)
    ld (07003h),a
    ld a,(07021h)
    ld (07004h),a
    call 01093h
    ld a,(0700dh)
    ld (07024h),a
    ld a,(0700eh)
    ld (07025h),a
    ld a,(07024h)
    ld (07022h),a
    ld a,(07025h)
    ld (07023h),a
    ld a,(07022h)
    ld (0700dh),a
    ld a,(07023h)
    ld (0700eh),a
    ret
```

### sdcc

```asm
    add hl,hl
    ex de,hl
    ret
    xor a
    sub l
    ld e,a
    sbc a,a
    sub h
    ld d,a
    ret
    ld c,l
    ld b,h
    push bc
    ex de,hl
    pop iy
    jp (iy)
    or a
    jr z,$+8
    ld hl,00000h
    jp 0000ah
    ld hl,00003h
    jp 0000ah
```

## max3

| kompilator | bajty funkcji | instrukcje | wołane procedury zewnętrzne |
| --- | ---: | ---: | --- |
| nasz | 154 | 56 | - |
| sdcc | 53 | 29 | - |

stosunek nasz / sdcc: 2.91

- sdcc: adresy wywołań zewnętrznych w .rel są niezrelokowane (widoczne jako 0000)

### nasz

```asm
    ld a,(07001h)
    ld (07011h),a
    ld a,(07002h)
    ld (07012h),a
    ld a,(07003h)
    ld (07013h),a
    ld a,(07004h)
    ld (07014h),a
    ld a,(07005h)
    ld (07015h),a
    ld a,(07006h)
    ld (07016h),a
    ld a,(07011h)
    ld (07017h),a
    ld a,(07012h)
    ld (07018h),a
    xor 080h
    ld (0700fh),a
    ld a,(07014h)
    xor 080h
    ld (07010h),a
    ld a,(07011h)
    ld hl,07013h
    sub (hl)
    ld a,(0700fh)
    ld hl,07010h
    sbc a,(hl)
    jp nc,01098h
    ld a,(07013h)
    ld (07017h),a
    ld a,(07014h)
    ld (07018h),a
    jp 01098h
    ld a,(07018h)
    xor 080h
    ld (0700fh),a
    ld a,(07016h)
    xor 080h
    ld (07010h),a
    ld a,(07017h)
    ld hl,07015h
    sub (hl)
    ld a,(0700fh)
    ld hl,07010h
    sbc a,(hl)
    jp nc,010c8h
    ld a,(07015h)
    ld (07017h),a
    ld a,(07016h)
    ld (07018h),a
    jp 010c8h
    ld a,(07017h)
    ld (0700dh),a
    ld a,(07018h)
    ld (0700eh),a
    ret
```

### sdcc

```asm
    push ix
    ld ix,00000h
    add ix,sp
    ld c,l
    ld b,h
    ld a,l
    sub e
    ld a,h
    sbc a,d
    jp po,00013h
    xor 080h
    jp p,00018h
    ld c,e
    ld b,d
    ld a,c
    sub (ix+004h)
    ld a,b
    sbc a,(ix+005h)
    jp po,00025h
    xor 080h
    jp p,0002eh
    ld c,(ix+004h)
    ld b,(ix+005h)
    ld e,c
    ld d,b
    pop ix
    pop hl
    pop af
    jp (hl)
```

## mul16

| kompilator | bajty funkcji | instrukcje | wołane procedury zewnętrzne |
| --- | ---: | ---: | --- |
| nasz | 76 | 26 | - |
| sdcc | 3 | 1 | mulint |

stosunek nasz / sdcc: 25.33

- sdcc: adresy wywołań zewnętrznych w .rel są niezrelokowane (widoczne jako 0000)

### nasz

```asm
    ld a,(07001h)
    ld (07011h),a
    ld a,(07002h)
    ld (07012h),a
    ld a,(07003h)
    ld (07013h),a
    ld a,(07004h)
    ld (07014h),a
    ld a,(07011h)
    ld (07001h),a
    ld a,(07012h)
    ld (07002h),a
    ld a,(07013h)
    ld (07003h),a
    ld a,(07014h)
    ld (07004h),a
    call 01090h
    ld a,(0700dh)
    ld (07015h),a
    ld a,(0700eh)
    ld (07016h),a
    ld a,(07015h)
    ld (0700dh),a
    ld a,(07016h)
    ld (0700eh),a
    ret
```

### sdcc

```asm
    jp 00000h
```

## shift

| kompilator | bajty funkcji | instrukcje | wołane procedury zewnętrzne |
| --- | ---: | ---: | --- |
| nasz | 64 | 22 | - |
| sdcc | 21 | 13 | - |

stosunek nasz / sdcc: 3.05

- sdcc: adresy wywołań zewnętrznych w .rel są niezrelokowane (widoczne jako 0000)

### nasz

```asm
    ld a,(07001h)
    ld (07011h),a
    ld a,(07002h)
    ld (07012h),a
    ld a,(07003h)
    ld (07013h),a
    ld a,(07011h)
    ld (07001h),a
    ld a,(07012h)
    ld (07002h),a
    ld a,(07013h)
    ld (07003h),a
    call 01084h
    ld a,(0700dh)
    ld (07014h),a
    ld a,(0700eh)
    ld (07015h),a
    ld a,(07014h)
    ld (0700dh),a
    ld a,(07015h)
    ld (0700eh),a
    ret
```

### sdcc

```asm
    ex de,hl
    ld iy,00002h
    add iy,sp
    ld b,(iy+000h)
    inc b
    jr $+5
    ex de,hl
    add hl,hl
    ex de,hl
    djnz $-3
    pop hl
    inc sp
    jp (hl)
```

## str_len

| kompilator | bajty funkcji | instrukcje | wołane procedury zewnętrzne |
| --- | ---: | ---: | --- |
| nasz | 67 | 29 | - |
| sdcc | 14 | 10 | - |

stosunek nasz / sdcc: 4.79

- sdcc: adresy wywołań zewnętrznych w .rel są niezrelokowane (widoczne jako 0000)

### nasz

```asm
    ld a,(07001h)
    ld (07011h),a
    ld a,(07002h)
    ld (07012h),a
    ld a,000h
    ld (07013h),a
    ld (07014h),a
    ld hl,(07011h)
    ld a,(hl)
    ld (07015h),a
    cp 000h
    jp nz,0105eh
    jp 01071h
    ld hl,07013h
    inc (hl)
    jr nz,$+4
    inc hl
    inc (hl)
    ld hl,07011h
    inc (hl)
    jr nz,$+4
    inc hl
    inc (hl)
    jp 0104fh
    ld a,(07013h)
    ld (0700dh),a
    ld a,(07014h)
    ld (0700eh),a
    ret
```

### sdcc

```asm
    ex de,hl
    ld hl,00000h
    ld a,(de)
    or a
    jr z,$+6
    inc hl
    inc de
    jr $-6
    ex de,hl
    ret
```

## structs

| kompilator | bajty funkcji | instrukcje | wołane procedury zewnętrzne |
| --- | ---: | ---: | --- |
| nasz | 273 | 118 | - |
| sdcc | 107 | 75 | - |

stosunek nasz / sdcc: 2.55

- sdcc: adresy wywołań zewnętrznych w .rel są niezrelokowane (widoczne jako 0000)

### nasz

```asm
    ld a,(07001h)
    ld (07011h),a
    ld a,(07002h)
    ld (07012h),a
    ld a,(07003h)
    ld (07013h),a
    ld a,(07004h)
    ld (07014h),a
    ld hl,(07013h)
    ld a,(hl)
    ld (07015h),a
    inc hl
    ld a,(hl)
    ld (07016h),a
    ld hl,(07011h)
    ld a,(hl)
    ld (07017h),a
    inc hl
    ld a,(hl)
    ld (07018h),a
    ld a,(07015h)
    ld hl,07017h
    sub (hl)
    ld (07015h),a
    ld a,(07016h)
    ld hl,07018h
    sbc a,(hl)
    ld (07016h),a
    ld hl,(07013h)
    inc hl
    inc hl
    ld a,(hl)
    ld (07017h),a
    inc hl
    ld a,(hl)
    ld (07018h),a
    ld hl,(07011h)
    inc hl
    inc hl
    ld a,(hl)
    ld (07019h),a
    inc hl
    ld a,(hl)
    ld (0701ah),a
    ld a,(07017h)
    ld hl,07019h
    sub (hl)
    ld (07017h),a
    ld a,(07018h)
    ld hl,0701ah
    sbc a,(hl)
    ld (07018h),a
    ld a,(07015h)
    ld hl,07017h
    add a,(hl)
    ld (07015h),a
    ld a,(07016h)
    ld hl,07018h
    adc a,(hl)
    ld (07016h),a
    ld hl,(07013h)
    ld de,00004h
    add hl,de
    ld a,(hl)
    ld (07017h),a
    ld a,(07015h)
    ld hl,07017h
    add a,(hl)
    ld (07015h),a
    ld a,(07016h)
    adc a,000h
    ld (07016h),a
    ld a,(07015h)
    ld (0700dh),a
    ld a,(07016h)
    ld (0700eh),a
    ret
    ld a,(07001h)
    ld (0701bh),a
    ld a,(07002h)
    ld (0701ch),a
    ld a,(07003h)
    ld (0701dh),a
    ld a,(07004h)
    ld (0701eh),a
    ld hl,(0701bh)
    ld a,(hl)
    ld (0701fh),a
    inc hl
    ld a,(hl)
    ld (07020h),a
    ld a,(0701fh)
    ld hl,0701dh
    add a,(hl)
    ld (0701fh),a
    ld a,(07020h)
    ld hl,0701eh
    adc a,(hl)
    ld (07020h),a
    ld hl,(0701bh)
    ld a,(0701fh)
    ld (hl),a
    ld a,(07020h)
    inc hl
    ld (hl),a
    ld hl,(0701bh)
    ld de,00004h
    add hl,de
    ld a,(hl)
    ld (07021h),a
    ld hl,07021h
    inc (hl)
    ld hl,(0701bh)
    ld de,00004h
    add hl,de
    ld a,(07021h)
    ld (hl),a
    ret
```

### sdcc

```asm
    push ix
    ld ix,00000h
    add ix,sp
    push af
    push af
    ld (ix-002h),l
    ld (ix-001h),h
    ld l,e
    ld h,d
    ld c,(hl)
    inc hl
    ld b,(hl)
    ld l,(ix-002h)
    ld h,(ix-001h)
    ld a,(hl)
    inc hl
    ld h,(hl)
    ld l,a
    ld a,c
    sub l
    ld c,a
    ld a,b
    sbc a,h
    ld b,a
    ld l,e
    ld h,d
    inc hl
    inc hl
    ld a,(hl)
    ld (ix-004h),a
    inc hl
    ld a,(hl)
    ld (ix-003h),a
    ld l,(ix-002h)
    ld h,(ix-001h)
    inc hl
    inc hl
    ld a,(hl)
    inc hl
    ld h,(hl)
    ld l,a
    ld a,(ix-004h)
    sub l
    ld l,a
    ld a,(ix-003h)
    sbc a,h
    ld h,a
    add hl,bc
    ld c,l
    ld b,h
    ld hl,00004h
    add hl,de
    ld l,(hl)
    ld h,000h
    add hl,bc
    ex de,hl
    ld sp,ix
    pop ix
    ret
    ld a,(hl)
    inc hl
    ld b,(hl)
    dec hl
    add a,e
    ld c,a
    ld a,b
    adc a,d
    ld (hl),c
    inc hl
    ld (hl),a
    dec hl
    ld bc,00004h
    add hl,bc
    inc (hl)
    ret
```

## sum_bytes

| kompilator | bajty funkcji | instrukcje | wołane procedury zewnętrzne |
| --- | ---: | ---: | --- |
| nasz | 90 | 36 | - |
| sdcc | 37 | 24 | - |

stosunek nasz / sdcc: 2.43

- sdcc: adresy wywołań zewnętrznych w .rel są niezrelokowane (widoczne jako 0000)

### nasz

```asm
    ld a,(07001h)
    ld (07011h),a
    ld a,(07002h)
    ld (07012h),a
    ld a,(07003h)
    ld (07013h),a
    ld a,000h
    ld (07014h),a
    ld (07015h),a
    ld a,(07013h)
    cp 000h
    jp nz,01060h
    jp 01088h
    ld hl,(07011h)
    ld a,(hl)
    ld (07018h),a
    ld a,(07014h)
    ld hl,07018h
    add a,(hl)
    ld (07014h),a
    ld a,(07015h)
    adc a,000h
    ld (07015h),a
    ld hl,07011h
    inc (hl)
    jr nz,$+4
    inc hl
    inc (hl)
    ld hl,07013h
    dec (hl)
    jp 01055h
    ld a,(07014h)
    ld (0700dh),a
    ld a,(07015h)
    ld (0700eh),a
    ret
```

### sdcc

```asm
    push ix
    ld ix,00000h
    add ix,sp
    ex de,hl
    ld hl,00000h
    ld c,(ix+004h)
    ld a,c
    or a
    jr z,$+14
    ld a,(de)
    ld b,000h
    add a,l
    ld l,a
    ld a,b
    adc a,h
    ld h,a
    inc de
    dec c
    jr $-14
    ex de,hl
    pop ix
    pop hl
    inc sp
    jp (hl)
```

## sw

| kompilator | bajty funkcji | instrukcje | wołane procedury zewnętrzne |
| --- | ---: | ---: | --- |
| nasz | 152 | 57 | - |
| sdcc | 47 | 27 | - |

stosunek nasz / sdcc: 3.23

- sdcc: adresy wywołań zewnętrznych w .rel są niezrelokowane (widoczne jako 0000)

### nasz

```asm
    ld a,(07001h)
    ld (07011h),a
    ld (07012h),a
    ld a,000h
    ld (07013h),a
    ld a,(07012h)
    ld hl,07013h
    or (hl)
    jp z,010a2h
    ld a,(07012h)
    cp 001h
    jp nz,01066h
    ld a,(07013h)
    cp 000h
    jp nz,01066h
    jp 010aah
    ld a,(07012h)
    cp 002h
    jp nz,01079h
    ld a,(07013h)
    cp 000h
    jp nz,01079h
    jp 010b2h
    ld a,(07012h)
    cp 003h
    jp nz,0108ch
    ld a,(07013h)
    cp 000h
    jp nz,0108ch
    jp 010bah
    ld a,(07012h)
    cp 004h
    jp nz,0109fh
    ld a,(07013h)
    cp 000h
    jp nz,0109fh
    jp 010c2h
    jp 010cah
    ld a,00ah
    ld (0700dh),a
    jp 010d2h
    ld a,014h
    ld (0700dh),a
    jp 010d2h
    ld a,023h
    ld (0700dh),a
    jp 010d2h
    ld a,029h
    ld (0700dh),a
    jp 010d2h
    ld a,039h
    ld (0700dh),a
    jp 010d2h
    ld a,000h
    ld (0700dh),a
    jp 010d2h
    ret
```

### sdcc

```asm
    ld c,a
    ld a,004h
    sub c
    jr c,$+41
    ld b,000h
    ld hl,0000fh
    add hl,bc
    add hl,bc
    add hl,bc
    jp (hl)
    jp 0001eh
    jp 00021h
    jp 00024h
    jp 00027h
    jp 0002ah
    ld a,00ah
    ret
    ld a,014h
    ret
    ld a,023h
    ret
    ld a,029h
    ret
    ld a,039h
    ret
    xor a
    ret
```
