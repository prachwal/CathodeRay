# Najczęstsze pary i trójki instrukcji 6502

Całkowita liczba instrukcji: 717

## Top 15 par instrukcji

| Para | Liczba |
| --- | ---: |
| `sta ZP ; lda ZP` | 128 |
| `lda ZP ; sta ZP` | 95 |
| `ldy #I ; lda M` | 16 |
| `sta ZP ; sta ZP` | 14 |
| `lda M ; sta ZP` | 14 |
| `lda ZP ; clc` | 14 |
| `sta ZP ; ldy #I` | 14 |
| `adc ZP ; sta ZP` | 13 |
| `lda #I ; sta ZP` | 13 |
| `sta ZP ; rts` | 13 |
| `rts ; sta ZP` | 12 |
| `lda ZP ; sec` | 11 |
| `jmp M ; lda ZP` | 10 |
| `sec ; sbc ZP` | 9 |
| `sta ZP ; lda #I` | 8 |

## Top 10 trójek instrukcji

| Trójka | Liczba |
| --- | ---: |
| `sta ZP ; lda ZP ; sta ZP` | 68 |
| `lda ZP ; sta ZP ; lda ZP` | 57 |
| `ldy #I ; lda M ; sta ZP` | 12 |
| `sta ZP ; lda ZP ; clc` | 11 |
| `rts ; sta ZP ; lda ZP` | 11 |
| `adc ZP ; sta ZP ; lda ZP` | 10 |
| `lda ZP ; sta ZP ; rts` | 10 |
| `lda ZP ; sta ZP ; sta ZP` | 9 |
| `sta ZP ; lda ZP ; sec` | 9 |
| `lda ZP ; sec ; sbc ZP` | 8 |
