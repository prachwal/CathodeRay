# Rozmiar kodu: mini-C, cc65, SDCC

Segment `CODE` w bajtach małych funkcji z `samples/bench` (jedna funkcja eksportowana na plik); wiersze z `*` mierzą
mnożenie, dzielenie i przesunięcie o zmienną liczbę — u mini-C razem z pomocnikiem w module, u cc65 i SDCC bez biblioteki.
Odświeżenie: `python3 tools/compare.py --write` (po `dotnet build`; wymaga cc65 i SDCC).

| funkcja | mini-C 6502 | cc65 -Os | mini-C Z80 | SDCC Z80 |
| --- | ---: | ---: | ---: | ---: |
| add32 | 46 | 22 | 36 | 33 |
| bubble | 446 | 271 | 270 | 276 |
| copy | 42 | 74 | 48 | 30 |
| div16* | 562 | 38 | 468 | 25 |
| fib | 144 | 52 | 95 | 31 |
| find | 119 | 80 | 74 | 106 |
| fnptr | 205 | 104 | 127 | 33 |
| max3 | 83 | 70 | 61 | 53 |
| mul16* | 91 | 19 | 64 | 3 |
| shift* | 199 | 27 | 155 | 21 |
| str_len | 43 | 56 | 27 | 14 |
| structs | 167 | 135 | 133 | 107 |
| sum_bytes | 54 | 71 | 40 | 37 |
| sw | 120 | 57 | 92 | 47 |

Średnia geometryczna bez wierszy z `*`: mini-C / cc65 = 1.36, mini-C / SDCC = 1.49.
