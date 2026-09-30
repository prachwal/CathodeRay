# Rozmiar kodu: mini-C, cc65, SDCC

Segment `CODE` w bajtach małych funkcji z `samples/bench` (jedna funkcja eksportowana na plik); wiersze z `*` mierzą
mnożenie, dzielenie i przesunięcie o zmienną liczbę — u mini-C razem z pomocnikiem w module, u cc65 i SDCC bez biblioteki.
Odświeżenie: `python3 tools/compare.py --write` (po `dotnet build`; wymaga cc65 i SDCC).

| funkcja | mini-C 6502 | cc65 -Os | mini-C Z80 | SDCC Z80 |
| --- | ---: | ---: | ---: | ---: |
| add32 | 46 | 22 | 33 | 33 |
| bubble | 446 | 271 | 268 | 276 |
| copy | 42 | 74 | 48 | 30 |
| div16* | 562 | 38 | 400 | 25 |
| fib | 144 | 52 | 78 | 31 |
| find | 119 | 80 | 69 | 106 |
| fnptr | 181 | 104 | 98 | 33 |
| max3 | 83 | 70 | 55 | 53 |
| mul16* | 75 | 19 | 45 | 3 |
| shift* | 187 | 27 | 133 | 21 |
| str_len | 43 | 56 | 25 | 14 |
| structs | 167 | 135 | 131 | 107 |
| sum_bytes | 54 | 71 | 38 | 37 |
| sw | 120 | 57 | 78 | 47 |

Średnia geometryczna bez wierszy z `*`: mini-C / cc65 = 1.34, mini-C / SDCC = 1.36.
