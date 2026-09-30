# Rozmiar kodu ścieżki VReg na celach

Segment `CODE` w bajtach po linkowaniu z biblioteką standardową (`cathode cc --stats --ir vreg`); osobny golden obok bramki Cell (`TargetSizeTests`). Tabelę odświeża
`UPDATE_VREG_SIZES=1 dotnet test --filter VRegSizeTests`.

Stan przy wprowadzeniu (plan 37): 48/192 pól różni się od Cell, zwykle w dół (np. `05_calls.c` 521→353 B na 6502 — druga runda CSE/DCE po podniesieniu); wyjątki w górę na Z80/8080 (`04_int_ops.c` +21/+27 B, `23_types.c` +44/+27 B — fazowanie z `RegisterAllocator`, do zbadania osobno). Domyślna ścieżka Cell nie drgnęła.

| program | stub | 6502 | 65c02 | nes | 6510 | z80 | 8080 | 6800 |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| 01_types.c | 455 | 236 | 236 | 236 | 236 | 126 | 143 | 177 |
| 02_control.c | 585 | 310 | 310 | 310 | 310 | 168 | 196 | 233 |
| 04_int_ops.c | 1058 | 625 | 625 | 625 | 625 | 483 | 558 | 520 |
| 05_calls.c | 616 | 353 | 353 | 353 | 353 | 193 | 228 | 286 |
| 06_defines.c | 301 | 183 | 183 | 183 | 183 | 89 | 104 | 104 |
| 07_control.c | 718 | 412 | 412 | 412 | 412 | 203 | 245 | 315 |
| 08_int_signed.c | 1096 | 954 | 954 | 954 | 954 | 635 | 861 | 1120 |
| 09_init.c | 979 | 805 | 805 | 805 | 805 | 559 | 760 | 924 |
| 10_struct.c | 895 | 413 | 413 | 413 | 413 | 295 | 326 | 304 |
| 11_goto_typedef.c | 998 | 506 | 506 | 506 | 506 | 344 | 360 | 427 |
| 12_globals_init.c | 1048 | 579 | 579 | 579 | 579 | 428 | 508 | 415 |
| 13_strings.c | 4428 | 2270 | 2270 | 2270 | 2270 | 1740 | 2104 | 2463 |
| 14_macros.c | 218 | 131 | 131 | 131 | 131 | 60 | 66 | 56 |
| 15_funcptr.c | 1430 | 809 | 809 | 809 | 809 | 608 | 698 | 856 |
| 16_printf.c | 7451 | 4048 | 4048 | 4048 | 4048 | 3391 | 3775 | 4551 |
| 17_casts.c | 516 | 331 | 331 | 331 | 331 | 225 | 272 | 257 |
| 18_voidptr.c | 2063 | 857 | 857 | 857 | 857 | 555 | 683 | 910 |
| 19_struct_value.c | 6487 | 2414 | 2414 | 2414 | 2414 | 2206 | 2323 | 2489 |
| 20_matrix.c | 2384 | 1296 | 1296 | 1296 | 1296 | 986 | 1080 | 1097 |
| 21_long.c | 10321 | 5871 | 5871 | 5871 | 5871 | 4810 | 5292 | 6229 |
| 22_misc.c | 1416 | 1031 | 1031 | 1031 | 1031 | 843 | 1007 | 1197 |
| 23_types.c | 2496 | 1901 | 1901 | 1901 | 1901 | 1702 | 1901 | 2054 |
| 24_float.c | 22759 | 15439 | 15439 | 15439 | 15439 | 14601 | 14900 | 15458 |
| 25_longlong.c | 17684 | 11438 | 11438 | 11438 | 11438 | 10408 | 10705 | 10932 |
