# Rozmiar kodu na celach

Segment `CODE` w bajtach po linkowaniu z biblioteką standardową (`cathode cc --stats`); tabelę odświeża
`UPDATE_TARGET_SIZES=1 dotnet test --filter TargetSizeTests`. Poprawność wyników sprawdza `TargetMatrixTests`.

| program | stub | 6502 | 65c02 | z80 | 8080 | 6800 |
| --- | ---: | ---: | ---: | ---: | ---: | ---: |
| 01_types.c | 474 | 334 | 334 | 283 | 283 | 265 |
| 02_control.c | 585 | 419 | 419 | 351 | 351 | 345 |
| 04_int_ops.c | 1210 | 965 | 965 | 888 | 888 | 876 |
| 05_calls.c | 1141 | 1543 | 1543 | 1514 | 1496 | 1463 |
| 06_defines.c | 301 | 195 | 195 | 138 | 138 | 128 |
| 07_control.c | 667 | 475 | 475 | 407 | 407 | 405 |
| 08_int_signed.c | 1205 | 1846 | 1846 | 1775 | 1758 | 1763 |
| 09_init.c | 979 | 1099 | 1099 | 1030 | 1017 | 998 |
| 10_struct.c | 708 | 457 | 457 | 384 | 382 | 364 |
| 11_goto_typedef.c | 1012 | 1127 | 1127 | 1070 | 1054 | 1043 |
| 12_globals_init.c | 948 | 572 | 572 | 508 | 505 | 487 |
| 13_strings.c | 4048 | 4148 | 4148 | 3687 | 3671 | 3717 |
| 14_macros.c | 243 | 151 | 151 | 98 | 98 | 86 |
| 15_funcptr.c | 1433 | 1030 | 1030 | 949 | 944 | 924 |
| 16_printf.c | 5696 | 6331 | 6331 | 6026 | 5956 | 5968 |
| 17_casts.c | 528 | 446 | 446 | 388 | 388 | 365 |
| 18_voidptr.c | 2078 | 1608 | 1608 | 1437 | 1432 | 1430 |
| 19_struct_value.c | 5413 | 3096 | 3096 | 2912 | 2909 | 2861 |
| 20_matrix.c | 2289 | 1611 | 1611 | 1467 | 1456 | 1414 |
| 21_long.c | 10388 | 10532 | 10532 | 10305 | 10151 | 10130 |
| 22_misc.c | 1416 | 1821 | 1821 | 1702 | 1686 | 1677 |
