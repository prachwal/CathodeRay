# Rozmiar kodu: mini-C, cc65, SDCC

Segment `CODE` w bajtach małych funkcji z `samples/bench` (jedna funkcja eksportowana na plik); wiersze z `*` mierzą
mnożenie, dzielenie i przesunięcie o zmienną liczbę — u mini-C razem z pomocnikiem w module, u cc65 i SDCC bez biblioteki.
Odświeżenie: `python3 tools/compare.py --write` (po `dotnet build`; wymaga cc65 i SDCC).

| funkcja | mini-C 6502 | cc65 -Os | mini-C Z80 | SDCC Z80 |
| --- | ---: | ---: | ---: | ---: |
| add32 | 169 | 22 | 170 | 33 |
| bubble | 684 | 271 | 609 | 276 |
| copy | 152 | 74 | 121 | 30 |
| div16* | 1134 | 38 | 1136 | 25 |
| fib | 238 | 52 | 234 | 31 |
| find | 222 | 80 | 207 | 106 |
| fnptr | 307 | 104 | 302 | 33 |
| max3 | 159 | 70 | 157 | 53 |
| mul16* | 286 | 19 | 283 | 3 |
| shift* | 158 | 27 | 157 | 21 |
| str_len | 104 | 56 | 88 | 14 |
| structs | 378 | 135 | 277 | 107 |
| sum_bytes | 120 | 71 | 104 | 37 |
| sw | 184 | 57 | 164 | 47 |

Średnia geometryczna bez wierszy z `*`: mini-C / cc65 = 2.84, mini-C / SDCC = 3.87.
