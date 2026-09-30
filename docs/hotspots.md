# Najczęstsze pary i trójki instrukcji 6502

Całkowita liczba instrukcji: 592

## Top 15 par instrukcji

| Para | Liczba |
| --- | ---: |
| `sta ZP ; lda ZP` | 92 |
| `lda ZP ; sta ZP` | 56 |
| `ldy #I ; lda M` | 17 |
| `lda M ; sta ZP` | 15 |
| `adc ZP ; sta ZP` | 14 |
| `lda ZP ; clc` | 13 |
| `sta ZP ; rts` | 12 |
| `sta ZP ; ldy #I` | 12 |
| `jmp M ; lda ZP` | 10 |
| `lda ZP ; sec` | 9 |
| `cmp #I ; bne M` | 9 |
| `lda ZP ; adc ZP` | 8 |
| `eor #I ; sta ZP` | 8 |
| `lda ZP ; eor #I` | 8 |
| `lda ZP ; cmp #I` | 8 |

## Top 10 trójek instrukcji

| Trójka | Liczba |
| --- | ---: |
| `sta ZP ; lda ZP ; sta ZP` | 33 |
| `lda ZP ; sta ZP ; lda ZP` | 30 |
| `ldy #I ; lda M ; sta ZP` | 15 |
| `adc ZP ; sta ZP ; lda ZP` | 12 |
| `sta ZP ; lda ZP ; clc` | 11 |
| `lda ZP ; sta ZP ; rts` | 9 |
| `eor #I ; sta ZP ; lda ZP` | 8 |
| `sta ZP ; ldy #I ; lda M` | 8 |
| `lda ZP ; adc ZP ; sta ZP` | 7 |
| `sta ZP ; lda ZP ; sec` | 7 |
