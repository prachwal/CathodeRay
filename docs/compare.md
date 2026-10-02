# Rozmiar kodu: mini-C, cc65, SDCC

Segment `CODE` w bajtach małych funkcji z `samples/bench` (jedna funkcja eksportowana na plik); wiersze z `*` mierzą
mnożenie, dzielenie i przesunięcie o zmienną liczbę — u mini-C razem z pomocnikiem w module, u cc65 i SDCC bez biblioteki.
Odświeżenie: `python3 tools/compare.py --write` (po `dotnet build`; wymaga cc65 i SDCC).

| funkcja | mini-C 6502 | cc65 -Os | mini-C Z80 | SDCC Z80 |
| --- | ---: | ---: | ---: | ---: |
| add32 | 46 | 22 | 33 | 33 |
| bubble | 432 | 271 | 221 | 276 |
| copy | 42 | 74 | 22 | 30 |
| div16* | 562 | 38 | 353 | 25 |
| fib | 142 | 52 | 68 | 31 |
| find | 115 | 80 | 62 | 106 |
| fnptr | 148 | 104 | 67 | 33 |
| max3 | 77 | 70 | 55 | 53 |
| mul16* | 42 | 19 | 28 | 3 |
| shift* | 158 | 27 | 110 | 21 |
| str_len | 43 | 56 | 25 | 14 |
| structs | 167 | 135 | 130 | 107 |
| sum_bytes | 54 | 71 | 38 | 37 |
| sw | 91 | 57 | 64 | 47 |

Średnia geometryczna bez wierszy z `*`: mini-C / cc65 = 1.27, mini-C / SDCC = 1.15.
