# Deasemblacja: nasz kompilator vs SDCC (z80)

## add32

| kompilator | bajty funkcji | instrukcje | wołane procedury zewnętrzne |
| --- | ---: | ---: | --- |
| nasz | 36 | 13 | - |
| sdcc | 33 | 19 | - |

stosunek nasz / sdcc: 1.09

- sdcc: adresy wywołań zewnętrznych w .rel są niezrelokowane (widoczne jako 0000)

### nasz

```asm
    ld hl,(07001h)
    ld de,(07005h)
    add hl,de
    ld (07011h),hl
    ld hl,(07003h)
    ld de,(07007h)
    adc hl,de
    ld (07013h),hl
    ld hl,(07013h)
    ld (08040h),hl
    ld hl,(07011h)
    ld (0700dh),hl
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
| nasz | 270 | 173 | - |
| sdcc | 276 | 111 | - |

stosunek nasz / sdcc: 0.98

- sdcc: adresy wywołań zewnętrznych w .rel są niezrelokowane (widoczne jako 0000)

### nasz

```asm
    ld hl,00000h
    ld (07011h),hl
    ld hl,(07003h)
    dec hl
    ld c,l
    ld b,h
    ld a,(07011h)
    sub c
    ld a,(07012h)
    sbc a,b
    jp po,01054h
    xor 080h
    jp p,01148h
    ld hl,00000h
    ld (07013h),hl
    ld hl,(07003h)
    dec hl
    ld c,l
    ld b,h
    ld l,c
    ld h,b
    push de
    ld de,(07011h)
    or a
    sbc hl,de
    pop de
    ld c,l
    ld b,h
    ld a,(07013h)
    sub c
    ld a,(07014h)
    sbc a,b
    jp po,0107dh
    xor 080h
    jp p,0113dh
    ld bc,(07013h)
    ld a,c
    sla a
    ld c,a
    ld a,b
    rl a
    ld b,a
    ld hl,(07001h)
    add hl,bc
    ld e,l
    ld d,h
    ld l,e
    ld h,d
    ld a,(hl)
    ld e,a
    inc hl
    ld a,(hl)
    ld d,a
    ld hl,(07013h)
    inc hl
    ld c,l
    ld b,h
    ld a,c
    sla a
    ld c,a
    ld a,b
    rl a
    ld b,a
    ld hl,(07001h)
    add hl,bc
    ld c,l
    ld b,h
    ld l,c
    ld h,b
    ld a,(hl)
    ld c,a
    inc hl
    ld a,(hl)
    ld b,a
    ld a,c
    sub e
    ld a,b
    sbc a,d
    jp po,010bdh
    xor 080h
    jp p,01132h
    ld bc,(07013h)
    ld a,c
    sla a
    ld c,a
    ld a,b
    rl a
    ld b,a
    ld hl,(07001h)
    add hl,bc
    ld e,l
    ld d,h
    ld l,e
    ld h,d
    ld a,(hl)
    ld (07015h),a
    inc hl
    ld a,(hl)
    ld (07016h),a
    ld hl,(07013h)
    inc hl
    ld c,l
    ld b,h
    ld a,c
    sla a
    ld c,a
    ld a,b
    rl a
    ld b,a
    ld hl,(07001h)
    add hl,bc
    ld e,l
    ld d,h
    ld l,e
    ld h,d
    ld a,(hl)
    ld e,a
    inc hl
    ld a,(hl)
    ld d,a
    ld bc,(07013h)
    ld a,c
    sla a
    ld c,a
    ld a,b
    rl a
    ld b,a
    ld hl,(07001h)
    add hl,bc
    ld c,l
    ld b,h
    ld l,c
    ld h,b
    ld a,e
    ld (hl),a
    ld a,d
    inc hl
    ld (hl),a
    ld hl,(07013h)
    inc hl
    ld c,l
    ld b,h
    ld a,c
    sla a
    ld c,a
    ld a,b
    rl a
    ld b,a
    ld hl,(07001h)
    add hl,bc
    ld c,l
    ld b,h
    ld l,c
    ld h,b
    ld a,(07015h)
    ld (hl),a
    ld a,(07016h)
    inc hl
    ld (hl),a
    jr $+2
    ld hl,07013h
    inc (hl)
    jr nz,$+4
    inc hl
    inc (hl)
    jp 0105dh
    ld hl,07011h
    inc (hl)
    jr nz,$+4
    inc hl
    inc (hl)
    jp 01041h
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
| nasz | 48 | 29 | - |
| sdcc | 30 | 18 | - |

stosunek nasz / sdcc: 1.60

- sdcc: adresy wywołań zewnętrznych w .rel są niezrelokowane (widoczne jako 0000)

### nasz

```asm
    ld a,(07005h)
    ld hl,07006h
    or (hl)
    jr z,$+40
    ld hl,(07003h)
    ld a,(hl)
    ld c,a
    ld hl,(07001h)
    ld a,c
    ld (hl),a
    ld hl,07001h
    inc (hl)
    jr nz,$+4
    inc hl
    inc (hl)
    ld hl,07003h
    inc (hl)
    jr nz,$+4
    inc hl
    inc (hl)
    ld hl,07005h
    ld a,(hl)
    dec (hl)
    or a
    jr nz,$+4
    inc hl
    dec (hl)
    jr $-45
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
| nasz | 62 | 25 | - |
| sdcc | 25 | 19 | divsint, modsint |

stosunek nasz / sdcc: 2.48

- sdcc: adresy wywołań zewnętrznych w .rel są niezrelokowane (widoczne jako 0000)

### nasz

```asm
    ld hl,(07001h)
    ld (07011h),hl
    ld hl,(07003h)
    ld (07013h),hl
    ld hl,(07011h)
    ld (07001h),hl
    ld hl,(07013h)
    ld (07003h),hl
    call 01080h
    ld bc,(0700dh)
    ld hl,(07011h)
    ld (07001h),hl
    ld hl,(07013h)
    ld (07003h),hl
    push bc
    call 0112bh
    pop bc
    ld de,(0700dh)
    ld l,c
    ld h,b
    add hl,de
    ld c,l
    ld b,h
    ld (0700dh),bc
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
| nasz | 95 | 44 | - |
| sdcc | 31 | 25 | - |

stosunek nasz / sdcc: 3.06

- sdcc: adresy wywołań zewnętrznych w .rel są niezrelokowane (widoczne jako 0000)

### nasz

```asm
    ld a,(07011h)
    push af
    ld a,(07012h)
    push af
    ld hl,(07001h)
    ld (07011h),hl
    ld a,(07011h)
    sub 002h
    ld a,(07012h)
    sbc a,000h
    jp po,01058h
    xor 080h
    jp p,01063h
    ld hl,(07011h)
    ld (0700dh),hl
    jr $+48
    ld hl,(07011h)
    dec hl
    ld e,l
    ld d,h
    ld (07001h),de
    call 0103bh
    ld bc,(0700dh)
    ld hl,(07011h)
    dec hl
    dec hl
    ld e,l
    ld d,h
    ld (07001h),de
    push bc
    call 0103bh
    pop bc
    ld de,(0700dh)
    ld l,c
    ld h,b
    add hl,de
    ld c,l
    ld b,h
    ld (0700dh),bc
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
| nasz | 74 | 44 | - |
| sdcc | 106 | 49 | - |

stosunek nasz / sdcc: 0.70

- sdcc: adresy wywołań zewnętrznych w .rel są niezrelokowane (widoczne jako 0000)

### nasz

```asm
    ld bc,00000h
    ld a,c
    ld hl,07003h
    sub (hl)
    ld a,b
    ld hl,07004h
    sbc a,(hl)
    jp po,0104dh
    xor 080h
    jp p,0107eh
    ld e,c
    ld d,b
    ld a,e
    sla a
    ld e,a
    ld a,d
    rl a
    ld d,a
    ld hl,(07001h)
    add hl,de
    ld e,l
    ld d,h
    ld l,e
    ld h,d
    ld a,(hl)
    ld e,a
    inc hl
    ld a,(hl)
    ld d,a
    ld a,e
    ld hl,07005h
    cp (hl)
    jr nz,$+15
    ld a,d
    ld hl,07006h
    cp (hl)
    jr nz,$+8
    ld (0700dh),bc
    jr $+11
    inc bc
    jr $-62
    ld hl,0ffffh
    ld (0700dh),hl
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
| nasz | 127 | 49 | - |
| sdcc | 33 | 22 | - |

stosunek nasz / sdcc: 3.85

- sdcc: adresy wywołań zewnętrznych w .rel są niezrelokowane (widoczne jako 0000)

### nasz

```asm
    ld hl,(07001h)
    ld de,(07001h)
    add hl,de
    ld c,l
    ld b,h
    ld (0700dh),bc
    ret
    ld a,000h
    ld hl,07001h
    sub (hl)
    ld c,a
    ld a,000h
    ld hl,07002h
    sbc a,(hl)
    ld b,a
    ld (0700dh),bc
    ret
    ld hl,(07001h)
    ld (07011h),hl
    ld hl,(07003h)
    ld (07013h),hl
    ld hl,(07013h)
    ld (07001h),hl
    ld hl,(07011h)
    call 0103ah
    ld bc,(0700dh)
    ld (0700dh),bc
    ret
    ld a,(07001h)
    ld (07015h),a
    ld hl,(07003h)
    ld (07016h),hl
    or a
    jr z,$+23
    ld hl,0103bh
    ld (07001h),hl
    ld hl,(07016h)
    ld (07003h),hl
    call 0105dh
    ld bc,(0700dh)
    jr $+21
    ld hl,0104ah
    ld (07001h),hl
    ld hl,(07016h)
    ld (07003h),hl
    call 0105dh
    ld bc,(0700dh)
    ld (0700dh),bc
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
| nasz | 61 | 25 | - |
| sdcc | 53 | 29 | - |

stosunek nasz / sdcc: 1.15

- sdcc: adresy wywołań zewnętrznych w .rel są niezrelokowane (widoczne jako 0000)

### nasz

```asm
    ld bc,(07001h)
    ld a,(07001h)
    ld hl,07003h
    sub (hl)
    ld a,(07002h)
    ld hl,07004h
    sbc a,(hl)
    jp po,01052h
    xor 080h
    jp p,0105bh
    ld bc,(07003h)
    jr $+2
    ld a,c
    ld hl,07005h
    sub (hl)
    ld a,b
    ld hl,07006h
    sbc a,(hl)
    jp po,0106ah
    xor 080h
    jp p,01073h
    ld bc,(07005h)
    jr $+2
    ld (0700dh),bc
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
| nasz | 36 | 12 | - |
| sdcc | 3 | 1 | mulint |

stosunek nasz / sdcc: 12.00

- sdcc: adresy wywołań zewnętrznych w .rel są niezrelokowane (widoczne jako 0000)

### nasz

```asm
    ld hl,(07001h)
    ld (07011h),hl
    ld hl,(07003h)
    ld (07013h),hl
    ld hl,(07011h)
    ld (07001h),hl
    ld hl,(07013h)
    ld (07003h),hl
    call 01066h
    ld bc,(0700dh)
    ld (0700dh),bc
    ret
```

### sdcc

```asm
    jp 00000h
```

## shift

| kompilator | bajty funkcji | instrukcje | wołane procedury zewnętrzne |
| --- | ---: | ---: | --- |
| nasz | 33 | 11 | - |
| sdcc | 21 | 13 | - |

stosunek nasz / sdcc: 1.57

- sdcc: adresy wywołań zewnętrznych w .rel są niezrelokowane (widoczne jako 0000)

### nasz

```asm
    ld hl,(07001h)
    ld (07011h),hl
    ld a,(07003h)
    ld (07013h),a
    ld hl,(07011h)
    ld (07001h),hl
    ld (07003h),a
    call 01063h
    ld bc,(0700dh)
    ld (0700dh),bc
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
| nasz | 27 | 15 | - |
| sdcc | 14 | 10 | - |

stosunek nasz / sdcc: 1.93

- sdcc: adresy wywołań zewnętrznych w .rel są niezrelokowane (widoczne jako 0000)

### nasz

```asm
    ld bc,00000h
    ld hl,(07001h)
    ld a,(hl)
    ld e,a
    or a
    jr z,$+13
    inc bc
    ld hl,07001h
    inc (hl)
    jr nz,$+4
    inc hl
    inc (hl)
    jr $-17
    ld (0700dh),bc
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
| nasz | 133 | 93 | - |
| sdcc | 107 | 75 | - |

stosunek nasz / sdcc: 1.24

- sdcc: adresy wywołań zewnętrznych w .rel są niezrelokowane (widoczne jako 0000)

### nasz

```asm
    ld hl,(07003h)
    ld a,(hl)
    ld c,a
    inc hl
    ld a,(hl)
    ld b,a
    ld hl,(07001h)
    ld a,(hl)
    ld e,a
    inc hl
    ld a,(hl)
    ld d,a
    ld l,c
    ld h,b
    or a
    sbc hl,de
    ld c,l
    ld b,h
    ld hl,(07003h)
    inc hl
    inc hl
    ld a,(hl)
    ld e,a
    inc hl
    ld a,(hl)
    ld d,a
    ld hl,(07001h)
    inc hl
    inc hl
    ld a,(hl)
    ld (07011h),a
    inc hl
    ld a,(hl)
    ld (07012h),a
    ld l,e
    ld h,d
    push de
    ld de,(07011h)
    or a
    sbc hl,de
    pop de
    ld e,l
    ld d,h
    ld l,c
    ld h,b
    add hl,de
    ld c,l
    ld b,h
    ld hl,(07003h)
    push de
    ld de,00004h
    add hl,de
    pop de
    ld a,(hl)
    ld e,a
    ld a,c
    add a,e
    ld c,a
    ld a,b
    adc a,000h
    ld b,a
    ld (0700dh),bc
    ret
    ld hl,(07001h)
    ld a,(hl)
    ld c,a
    inc hl
    ld a,(hl)
    ld b,a
    ld l,c
    ld h,b
    ld de,(07003h)
    add hl,de
    ld c,l
    ld b,h
    ld hl,(07001h)
    ld a,c
    ld (hl),a
    ld a,b
    inc hl
    ld (hl),a
    ld hl,(07001h)
    ld de,00004h
    add hl,de
    ld a,(hl)
    ld c,a
    inc c
    ld hl,(07001h)
    ld de,00004h
    add hl,de
    ld a,c
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
| nasz | 40 | 23 | - |
| sdcc | 37 | 24 | - |

stosunek nasz / sdcc: 1.08

- sdcc: adresy wywołań zewnętrznych w .rel są niezrelokowane (widoczne jako 0000)

### nasz

```asm
    ld bc,00000h
    ld a,(07003h)
    or a
    jr z,$+28
    ld hl,(07001h)
    ld a,(hl)
    ld e,a
    ld a,c
    add a,e
    ld c,a
    ld a,b
    adc a,000h
    ld b,a
    ld hl,07001h
    inc (hl)
    jr nz,$+4
    inc hl
    inc (hl)
    ld hl,07003h
    dec (hl)
    jr $-30
    ld (0700dh),bc
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
| nasz | 92 | 51 | - |
| sdcc | 47 | 27 | - |

stosunek nasz / sdcc: 1.96

- sdcc: adresy wywołań zewnętrznych w .rel są niezrelokowane (widoczne jako 0000)

### nasz

```asm
    ld a,(07001h)
    ld c,a
    ld a,000h
    ld b,a
    ld a,c
    or b
    jr z,$+40
    ld a,c
    cp 001h
    jr nz,$+6
    ld a,b
    or a
    jr z,$+38
    ld a,c
    cp 002h
    jr nz,$+6
    ld a,b
    or a
    jr z,$+36
    ld a,c
    cp 003h
    jr nz,$+6
    ld a,b
    or a
    jr z,$+34
    ld a,c
    cp 004h
    jr nz,$+6
    ld a,b
    or a
    jr z,$+32
    jr $+37
    ld a,00ah
    ld (0700dh),a
    jr $+37
    ld a,014h
    ld (0700dh),a
    jr $+30
    ld a,023h
    ld (0700dh),a
    jr $+23
    ld a,029h
    ld (0700dh),a
    jr $+16
    ld a,039h
    ld (0700dh),a
    jr $+9
    ld a,000h
    ld (0700dh),a
    jr $+2
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
