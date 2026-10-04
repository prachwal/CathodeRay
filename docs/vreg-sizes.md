# Rozmiar kodu ścieżki VReg na celach

Segment `CODE` w bajtach po linkowaniu z biblioteką standardową (`cathode cc --stats --ir vreg`); osobny golden obok bramki Cell (`TargetSizeTests`). Tabelę odświeża
`UPDATE_VREG_SIZES=1 dotnet test --filter VRegSizeTests`.

Stan przy wprowadzeniu (plan 37): 48/192 pól różni się od Cell, zwykle w dół (np. `05_calls.c` 521→353 B na 6502 — druga runda CSE/DCE po podniesieniu); wyjątki w górę na Z80/8080 (`04_int_ops.c` +21/+27 B, `23_types.c` +44/+27 B — fazowanie z `RegisterAllocator`, do zbadania osobno). Domyślna ścieżka Cell nie drgnęła.

| program | stub | 6502 | 65c02 | nes | 6510 | z80 | 8080 | 6800 | 6809 |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| 01_types.c | 455 | 236 | 236 | 236 | 236 | 126 | 143 | 177 | 183 |
| 02_control.c | 537 | 302 | 302 | 302 | 302 | 159 | 196 | 225 | 223 |
| 04_int_ops.c | 1001 | 609 | 609 | 609 | 609 | 432 | 508 | 504 | 510 |
| 05_calls.c | 610 | 349 | 349 | 349 | 349 | 163 | 200 | 283 | 297 |
| 06_defines.c | 292 | 179 | 179 | 179 | 179 | 87 | 104 | 100 | 106 |
| 07_control.c | 605 | 386 | 386 | 386 | 386 | 187 | 229 | 289 | 292 |
| 08_int_signed.c | 1078 | 954 | 954 | 954 | 954 | 585 | 780 | 1120 | 1136 |
| 09_init.c | 970 | 801 | 801 | 801 | 801 | 513 | 687 | 920 | 943 |
| 10_struct.c | 895 | 405 | 405 | 405 | 405 | 272 | 305 | 304 | 308 |
| 11_goto_typedef.c | 998 | 506 | 506 | 506 | 506 | 314 | 329 | 427 | 430 |
| 12_globals_init.c | 1048 | 579 | 579 | 579 | 579 | 383 | 469 | 415 | 421 |
| 13_strings.c | 4278 | 2224 | 2224 | 2224 | 2224 | 1654 | 2052 | 2434 | 2408 |
| 14_macros.c | 218 | 131 | 131 | 131 | 131 | 60 | 66 | 56 | 62 |
| 15_funcptr.c | 1430 | 766 | 766 | 766 | 766 | 602 | 688 | 856 | 861 |
| 16_printf.c | 7169 | 3958 | 3958 | 3958 | 3958 | 3200 | 3564 | 4482 | 4464 |
| 17_casts.c | 516 | 331 | 331 | 331 | 331 | 200 | 224 | 257 | 263 |
| 18_voidptr.c | 2054 | 857 | 857 | 857 | 857 | 535 | 649 | 910 | 893 |
| 19_struct_value.c | 6487 | 2397 | 2397 | 2397 | 2397 | 2112 | 2252 | 2489 | 2503 |
| 20_matrix.c | 2348 | 1280 | 1280 | 1280 | 1280 | 951 | 1062 | 1081 | 1081 |
| 21_long.c | 9919 | 5755 | 5755 | 5755 | 5755 | 4571 | 5041 | 6146 | 6119 |
| 22_misc.c | 1407 | 1019 | 1019 | 1019 | 1019 | 813 | 958 | 1184 | 1174 |
| 23_types.c | 2463 | 1883 | 1883 | 1883 | 1883 | 1670 | 1865 | 2036 | 2032 |
| 24_float.c | 22090 | 15355 | 15355 | 15355 | 15355 | 14154 | 14494 | 15381 | 15365 |
| 25_longlong.c | 17294 | 11399 | 11399 | 11399 | 11399 | 10024 | 10343 | 10910 | 10896 |
