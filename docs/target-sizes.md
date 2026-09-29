# Rozmiar kodu na celach

Segment `CODE` w bajtach po linkowaniu z biblioteką standardową (`cathode cc --stats`); tabelę odświeża
`UPDATE_TARGET_SIZES=1 dotnet test --filter TargetSizeTests`. Poprawność wyników sprawdza `TargetMatrixTests`.

| program | stub | 6502 | 65c02 | z80 | 8080 | 6800 |
| --- | ---: | ---: | ---: | ---: | ---: | ---: |
| 01_types.c | 474 | 334 | 334 | 283 | 283 | 265 |
| 02_control.c | 585 | 369 | 369 | 305 | 309 | 299 |
| 04_int_ops.c | 1210 | 907 | 907 | 845 | 845 | 818 |
| 05_calls.c | 1141 | 1435 | 1435 | 1411 | 1393 | 1356 |
| 06_defines.c | 301 | 193 | 193 | 136 | 136 | 126 |
| 07_control.c | 667 | 460 | 460 | 392 | 393 | 391 |
| 08_int_signed.c | 1205 | 1683 | 1683 | 1641 | 1628 | 1606 |
| 09_init.c | 979 | 1022 | 1022 | 965 | 953 | 923 |
| 10_struct.c | 708 | 419 | 419 | 350 | 349 | 327 |
| 11_goto_typedef.c | 1012 | 1042 | 1042 | 1000 | 984 | 961 |
| 12_globals_init.c | 948 | 531 | 531 | 474 | 472 | 447 |
| 13_strings.c | 4048 | 3723 | 3723 | 3332 | 3329 | 3311 |
| 14_macros.c | 243 | 151 | 151 | 98 | 98 | 86 |
| 15_funcptr.c | 1433 | 987 | 987 | 914 | 909 | 882 |
| 16_printf.c | 5696 | 5838 | 5838 | 5611 | 5548 | 5498 |
| 17_casts.c | 528 | 428 | 428 | 373 | 373 | 347 |
| 18_voidptr.c | 2078 | 1435 | 1435 | 1289 | 1294 | 1267 |
| 19_struct_value.c | 5413 | 2707 | 2707 | 2537 | 2537 | 2475 |
| 20_matrix.c | 2289 | 1529 | 1529 | 1395 | 1388 | 1336 |
| 21_long.c | 10388 | 9781 | 9781 | 9669 | 9523 | 9406 |
| 22_misc.c | 1416 | 1616 | 1616 | 1525 | 1515 | 1482 |
