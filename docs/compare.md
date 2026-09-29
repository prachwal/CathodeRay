# Rozmiar kodu: mini-C, cc65, SDCC

Segment `CODE` w bajtach małych funkcji z `samples/bench` (jedna funkcja eksportowana na plik); wiersze z `*` mierzą
mnożenie, dzielenie i przesunięcie o zmienną liczbę — u mini-C razem z pomocnikiem w module, u cc65 i SDCC bez biblioteki.
Odświeżenie: `python3 tools/compare.py --write` (po `dotnet build`; wymaga cc65 i SDCC).

| funkcja | mini-C 6502 | cc65 -Os | mini-C Z80 | SDCC Z80 |
| --- | ---: | ---: | ---: | ---: |
| add32 | 119 | 22 | 170 | 33 |
| bubble | 462 | 271 | 586 | 276 |
| copy | 66 | 74 | 90 | 30 |
| div16* | 586 | 38 | 747 | 25 |
| fib | 171 | 52 | 231 | 31 |
| find | 146 | 80 | 195 | 106 |
| fnptr | 229 | 104 | 302 | 33 |
| max3 | 107 | 70 | 154 | 53 |
| mul16* | 91 | 19 | 104 | 3 |
| shift* | 239 | 27 | 343 | 21 |
| str_len | 51 | 56 | 67 | 14 |
| structs | 203 | 135 | 273 | 107 |
| sum_bytes | 66 | 71 | 90 | 37 |
| sw | 122 | 57 | 152 | 47 |

Średnia geometryczna bez wierszy z `*`: mini-C / cc65 = 1.74, mini-C / SDCC = 3.56.
