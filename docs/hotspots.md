# Najczęstsze pary i trójki instrukcji 6502

Całkowita liczba instrukcji: 600

## Top 15 par instrukcji

| Para | Liczba |
| --- | ---: |
| `sta ZP ; lda ZP` | 98 |
| `lda ZP ; sta ZP` | 63 |
| `ldy #I ; lda M` | 16 |
| `lda M ; sta ZP` | 14 |
| `sta ZP ; ldy #I` | 14 |
| `lda ZP ; clc` | 13 |
| `lda #I ; sta ZP` | 13 |
| `adc ZP ; sta ZP` | 12 |
| `lda ZP ; sec` | 11 |
| `sta ZP ; sta ZP` | 10 |
| `sec ; sbc ZP` | 9 |
| `sta ZP ; lda #I` | 8 |
| `rts ; sta ZP` | 8 |
| `jmp M ; lda ZP` | 8 |
| `sta ZP ; rts` | 8 |

## Top 10 trójek instrukcji

| Trójka | Liczba |
| --- | ---: |
| `sta ZP ; lda ZP ; sta ZP` | 46 |
| `lda ZP ; sta ZP ; lda ZP` | 37 |
| `ldy #I ; lda M ; sta ZP` | 12 |
| `sta ZP ; lda ZP ; clc` | 11 |
| `adc ZP ; sta ZP ; lda ZP` | 9 |
| `sta ZP ; lda ZP ; sec` | 9 |
| `lda ZP ; sec ; sbc ZP` | 8 |
| `rts ; sta ZP ; lda ZP` | 7 |
| `eor #I ; sta ZP ; lda ZP` | 7 |
| `lda ZP ; clc ; adc ZP` | 7 |
