# Rozmiar kodu na celach

Segment `CODE` w bajtach po linkowaniu z biblioteką standardową (`cathode cc --stats`); tabelę odświeża
`UPDATE_TARGET_SIZES=1 dotnet test --filter TargetSizeTests`. Poprawność wyników sprawdza `TargetMatrixTests`.

| program | stub | 6502 | 65c02 | z80 | 8080 | 6800 |
| --- | ---: | ---: | ---: | ---: | ---: | ---: |
| 01_types.c | 455 | 260 | 260 | 246 | 246 | 228 |
| 02_control.c | 585 | 310 | 310 | 305 | 309 | 281 |
| 04_int_ops.c | 1103 | 657 | 657 | 787 | 787 | 729 |
| 05_calls.c | 880 | 600 | 600 | 667 | 667 | 641 |
| 06_defines.c | 301 | 183 | 183 | 136 | 136 | 123 |
| 07_control.c | 730 | 420 | 420 | 439 | 440 | 411 |
| 08_int_signed.c | 1106 | 1024 | 1024 | 1229 | 1652 | 1577 |
| 09_init.c | 979 | 829 | 829 | 928 | 1353 | 1291 |
| 10_struct.c | 895 | 413 | 413 | 416 | 415 | 384 |
| 11_goto_typedef.c | 1001 | 536 | 536 | 636 | 624 | 587 |
| 12_globals_init.c | 1048 | 579 | 579 | 674 | 659 | 621 |
| 13_strings.c | 4443 | 2437 | 2437 | 2993 | 3422 | 3272 |
| 14_macros.c | 218 | 131 | 131 | 70 | 70 | 58 |
| 15_funcptr.c | 1433 | 928 | 928 | 1088 | 1206 | 1168 |
| 16_printf.c | 7466 | 5008 | 5008 | 5846 | 6233 | 5958 |
| 17_casts.c | 516 | 357 | 357 | 361 | 361 | 332 |
| 18_voidptr.c | 2066 | 958 | 958 | 1124 | 1282 | 1237 |
| 19_struct_value.c | 6538 | 2589 | 2589 | 2954 | 3093 | 3010 |
| 20_matrix.c | 2384 | 1344 | 1344 | 1484 | 1464 | 1401 |
| 21_long.c | 10336 | 7132 | 7132 | 8494 | 8836 | 8476 |
| 22_misc.c | 1416 | 1069 | 1069 | 1313 | 1704 | 1627 |
