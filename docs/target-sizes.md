# Rozmiar kodu na celach

Segment `CODE` w bajtach po linkowaniu z biblioteką standardową (`cathode cc --stats`); tabelę odświeża
`UPDATE_TARGET_SIZES=1 dotnet test --filter TargetSizeTests`. Poprawność wyników sprawdza `TargetMatrixTests`.

| program | stub | 6502 | 65c02 | z80 | 8080 | 6800 |
| --- | ---: | ---: | ---: | ---: | ---: | ---: |
| 01_types.c | 474 | 288 | 288 | 283 | 283 | 265 |
| 02_control.c | 585 | 310 | 310 | 305 | 309 | 281 |
| 04_int_ops.c | 1103 | 657 | 657 | 787 | 787 | 729 |
| 05_calls.c | 890 | 636 | 636 | 718 | 718 | 692 |
| 06_defines.c | 301 | 183 | 183 | 136 | 136 | 123 |
| 07_control.c | 667 | 375 | 375 | 392 | 393 | 364 |
| 08_int_signed.c | 1106 | 1253 | 1253 | 1554 | 1540 | 1465 |
| 09_init.c | 979 | 1058 | 1058 | 1253 | 1241 | 1179 |
| 10_struct.c | 708 | 356 | 356 | 350 | 349 | 324 |
| 11_goto_typedef.c | 999 | 1152 | 1152 | 1416 | 1400 | 1337 |
| 12_globals_init.c | 948 | 438 | 438 | 474 | 472 | 444 |
| 13_strings.c | 4048 | 2997 | 2997 | 3764 | 3761 | 3604 |
| 14_macros.c | 218 | 131 | 131 | 70 | 70 | 58 |
| 15_funcptr.c | 1433 | 799 | 799 | 914 | 909 | 879 |
| 16_printf.c | 7466 | 5639 | 5639 | 6632 | 6582 | 6297 |
| 17_casts.c | 516 | 357 | 357 | 361 | 361 | 332 |
| 18_voidptr.c | 2066 | 1041 | 1041 | 1277 | 1282 | 1237 |
| 19_struct_value.c | 5413 | 2213 | 2213 | 2606 | 2608 | 2535 |
| 20_matrix.c | 2289 | 1236 | 1236 | 1395 | 1388 | 1333 |
| 21_long.c | 9885 | 7444 | 7444 | 8933 | 8837 | 8479 |
| 22_misc.c | 1416 | 1573 | 1573 | 1957 | 1947 | 1862 |
