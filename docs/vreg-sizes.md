# Rozmiar kodu ścieżki VReg na celach

Segment `CODE` w bajtach po linkowaniu z biblioteką standardową (`cathode cc --stats --ir vreg`); osobny golden obok bramki Cell (`TargetSizeTests`). Tabelę odświeża
`UPDATE_VREG_SIZES=1 dotnet test --filter VRegSizeTests`.

Stan przy wprowadzeniu (plan 37): 48/192 pól różni się od Cell, zwykle w dół (np. `05_calls.c` 521→353 B na 6502 — druga runda CSE/DCE po podniesieniu); wyjątki w górę na Z80/8080 (`04_int_ops.c` +21/+27 B, `23_types.c` +44/+27 B — fazowanie z `RegisterAllocator`, do zbadania osobno). Domyślna ścieżka Cell nie drgnęła.

| program | stub | 6502 | 65c02 | nes | 6510 | z80 | 8080 | 6800 |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| 01_types.c | 455 | 236 | 236 | 236 | 236 | 126 | 143 | 177 |
| 02_control.c | 585 | 302 | 302 | 302 | 302 | 159 | 196 | 225 |
| 04_int_ops.c | 1058 | 609 | 609 | 609 | 609 | 468 | 549 | 504 |
| 05_calls.c | 616 | 349 | 349 | 349 | 349 | 180 | 219 | 283 |
| 06_defines.c | 301 | 179 | 179 | 179 | 179 | 87 | 104 | 100 |
| 07_control.c | 623 | 386 | 386 | 386 | 386 | 187 | 229 | 289 |
| 08_int_signed.c | 1096 | 954 | 954 | 954 | 954 | 628 | 834 | 1120 |
| 09_init.c | 979 | 801 | 801 | 801 | 801 | 556 | 741 | 920 |
| 10_struct.c | 895 | 413 | 413 | 413 | 413 | 292 | 325 | 304 |
| 11_goto_typedef.c | 998 | 506 | 506 | 506 | 506 | 333 | 353 | 427 |
| 12_globals_init.c | 1048 | 579 | 579 | 579 | 579 | 383 | 469 | 415 |
| 13_strings.c | 4428 | 2224 | 2224 | 2224 | 2224 | 1656 | 2059 | 2434 |
| 14_macros.c | 218 | 131 | 131 | 131 | 131 | 60 | 66 | 56 |
| 15_funcptr.c | 1430 | 766 | 766 | 766 | 766 | 602 | 688 | 856 |
| 16_printf.c | 7298 | 3958 | 3958 | 3958 | 3958 | 3227 | 3591 | 4482 |
| 17_casts.c | 516 | 331 | 331 | 331 | 331 | 221 | 271 | 257 |
| 18_voidptr.c | 2063 | 857 | 857 | 857 | 857 | 549 | 672 | 910 |
| 19_struct_value.c | 6487 | 2397 | 2397 | 2397 | 2397 | 2151 | 2289 | 2489 |
| 20_matrix.c | 2384 | 1280 | 1280 | 1280 | 1280 | 951 | 1062 | 1081 |
| 21_long.c | 10168 | 5765 | 5765 | 5765 | 5765 | 4604 | 5073 | 6144 |
| 22_misc.c | 1416 | 1019 | 1019 | 1019 | 1019 | 831 | 976 | 1184 |
| 23_types.c | 2496 | 1883 | 1883 | 1883 | 1883 | 1668 | 1851 | 2036 |
| 24_float.c | 22759 | 15355 | 15355 | 15355 | 15355 | 14172 | 14512 | 15381 |
| 25_longlong.c | 17684 | 11399 | 11399 | 11399 | 11399 | 10094 | 10407 | 10910 |
