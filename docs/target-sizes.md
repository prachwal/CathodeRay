# Rozmiar kodu na celach

Segment `CODE` w bajtach po linkowaniu z biblioteką standardową (`cathode cc --stats`); tabelę odświeża
`UPDATE_TARGET_SIZES=1 dotnet test --filter TargetSizeTests`. Poprawność wyników sprawdza `TargetMatrixTests`.

| program | stub | 6502 | 65c02 | z80 | 8080 | 6800 |
| --- | ---: | ---: | ---: | ---: | ---: | ---: |
| 01_types.c | 474 | 288 | 288 | 283 | 283 | 265 |
| 02_control.c | 585 | 310 | 310 | 305 | 309 | 281 |
| 04_int_ops.c | 1210 | 689 | 689 | 845 | 845 | 776 |
| 05_calls.c | 1141 | 1214 | 1214 | 1411 | 1393 | 1344 |
| 06_defines.c | 301 | 183 | 183 | 136 | 136 | 123 |
| 07_control.c | 667 | 375 | 375 | 392 | 393 | 364 |
| 08_int_signed.c | 1205 | 1455 | 1455 | 1641 | 1628 | 1552 |
| 09_init.c | 979 | 861 | 861 | 965 | 953 | 902 |
| 10_struct.c | 708 | 356 | 356 | 350 | 349 | 324 |
| 11_goto_typedef.c | 1012 | 884 | 884 | 1000 | 984 | 943 |
| 12_globals_init.c | 948 | 438 | 438 | 474 | 472 | 444 |
| 13_strings.c | 4048 | 2708 | 2708 | 3332 | 3329 | 3194 |
| 14_macros.c | 243 | 152 | 152 | 98 | 98 | 86 |
| 15_funcptr.c | 1433 | 830 | 830 | 914 | 909 | 879 |
| 16_printf.c | 5696 | 5052 | 5052 | 5611 | 5548 | 5333 |
| 17_casts.c | 528 | 369 | 369 | 373 | 373 | 344 |
| 18_voidptr.c | 2078 | 1098 | 1098 | 1289 | 1294 | 1249 |
| 19_struct_value.c | 5413 | 2286 | 2286 | 2537 | 2537 | 2466 |
| 20_matrix.c | 2289 | 1283 | 1283 | 1395 | 1388 | 1333 |
| 21_long.c | 10388 | 8449 | 8449 | 9669 | 9523 | 9151 |
| 22_misc.c | 1416 | 1282 | 1282 | 1525 | 1515 | 1452 |
