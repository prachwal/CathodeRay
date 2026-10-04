# Rozmiar kodu: mini-C, cc65, SDCC

Segment `CODE` w bajtach małych funkcji z `samples/bench` (jedna funkcja eksportowana na plik); wiersze z `*` mierzą
mnożenie, dzielenie i przesunięcie o zmienną liczbę — u mini-C razem z pomocnikiem w module, u cc65 i SDCC bez biblioteki.
Odświeżenie: `python3 tools/compare.py --write` (po `dotnet build`; wymaga cc65 i SDCC).

| funkcja | mini-C 6502 | cc65 -Os | mini-C Z80 | SDCC Z80 |
| --- | ---: | ---: | ---: | ---: |
| add32 | 46 | 22 | 33 | 33 |
| args_many | 80 | 83 | 37 | 56 |
| arith16 | 119 | 90 | 81 | 45 |
| branch_far | 259 | 628 | 409 | 264 |
| bubble | 432 | 271 | 221 | 276 |
| copy | 42 | 74 | 22 | 30 |
| div16* | 562 | 38 | 353 | 25 |
| fib | 142 | 52 | 57 | 31 |
| find | 115 | 80 | 62 | 106 |
| fnptr | 104 | 104 | 58 | 33 |
| index_struct | 173 | 112 | 108 | 68 |
| loop_u16 | 45 | 61 | 36 | 14 |
| loop_u8 | 27 | 51 | 27 | 23 |
| max3 | 77 | 70 | 55 | 53 |
| mul16* | 42 | 19 | 28 | 3 |
| ret_wide | 69 | 35 | 70 | 34 |
| shift* | 158 | 27 | 110 | 21 |
| str_len | 43 | 56 | 25 | 14 |
| strength | 119 | 47 | 63 | 18 |
| structs | 167 | 135 | 130 | 107 |
| sum_bytes | 54 | 71 | 38 | 37 |
| sw | 91 | 57 | 64 | 47 |
| sw_dense | 173 | 100 | 101 | 75 |
| sw_sparse | 91 | 59 | 65 | 40 |
| tail_chain | 104 | 50 | 46 | 20 |
| zp_pressure | 104 | 158 | 88 | 85 |

Średnia geometryczna bez wierszy z `*`: mini-C / cc65 = 1.19, mini-C / SDCC = 1.36.
