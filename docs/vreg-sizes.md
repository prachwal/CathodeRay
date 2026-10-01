# Rozmiar kodu ścieżki VReg na celach

Segment `CODE` w bajtach po linkowaniu z biblioteką standardową (`cathode cc --stats --ir vreg`); osobny golden obok bramki Cell (`TargetSizeTests`). Tabelę odświeża
`UPDATE_VREG_SIZES=1 dotnet test --filter VRegSizeTests`.

Stan przy wprowadzeniu (plan 37): 48/192 pól różni się od Cell, zwykle w dół (np. `05_calls.c` 521→353 B na 6502 — druga runda CSE/DCE po podniesieniu); wyjątki w górę na Z80/8080 (`04_int_ops.c` +21/+27 B, `23_types.c` +44/+27 B — fazowanie z `RegisterAllocator`, do zbadania osobno). Domyślna ścieżka Cell nie drgnęła.

| program | stub | 6502 | 65c02 | nes | 6510 | z80 | 8080 | 6800 |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| 01_types.c | 455 | 236 | 236 | 236 | 236 | 126 | 143 | 177 |
| 02_control.c | 585 | 310 | 310 | 310 | 310 | 168 | 196 | 233 |
| 04_int_ops.c | 1058 | 625 | 625 | 625 | 625 | 483 | 558 | 520 |
| 05_calls.c | 616 | 349 | 349 | 349 | 349 | 181 | 219 | 283 |
| 06_defines.c | 301 | 183 | 183 | 183 | 183 | 89 | 104 | 104 |
| 07_control.c | 718 | 412 | 412 | 412 | 412 | 203 | 245 | 315 |
| 08_int_signed.c | 1096 | 954 | 954 | 954 | 954 | 635 | 861 | 1120 |
| 09_init.c | 979 | 805 | 805 | 805 | 805 | 559 | 760 | 924 |
| 10_struct.c | 895 | 413 | 413 | 413 | 413 | 295 | 326 | 304 |
| 11_goto_typedef.c | 998 | 506 | 506 | 506 | 506 | 344 | 360 | 427 |
| 12_globals_init.c | 1048 | 579 | 579 | 579 | 579 | 428 | 508 | 415 |
| 13_strings.c | 4428 | 2260 | 2260 | 2260 | 2260 | 1726 | 2092 | 2454 |
| 14_macros.c | 218 | 131 | 131 | 131 | 131 | 60 | 66 | 56 |
| 15_funcptr.c | 1430 | 767 | 767 | 767 | 767 | 608 | 698 | 856 |
| 16_printf.c | 7451 | 4009 | 4009 | 4009 | 4009 | 3372 | 3754 | 4532 |
| 17_casts.c | 516 | 331 | 331 | 331 | 331 | 222 | 272 | 257 |
| 18_voidptr.c | 2063 | 857 | 857 | 857 | 857 | 555 | 683 | 910 |
| 19_struct_value.c | 6487 | 2398 | 2398 | 2398 | 2398 | 2206 | 2323 | 2489 |
| 20_matrix.c | 2384 | 1296 | 1296 | 1296 | 1296 | 983 | 1080 | 1097 |
| 21_long.c | 10321 | 5832 | 5832 | 5832 | 5832 | 4791 | 5271 | 6210 |
| 22_misc.c | 1416 | 1023 | 1023 | 1023 | 1023 | 832 | 995 | 1188 |
| 23_types.c | 2496 | 1883 | 1883 | 1883 | 1883 | 1677 | 1877 | 2036 |
| 24_float.c | 22759 | 15383 | 15383 | 15383 | 15383 | 14546 | 14844 | 15409 |
| 25_longlong.c | 17684 | 11399 | 11399 | 11399 | 11399 | 10382 | 10675 | 10910 |
