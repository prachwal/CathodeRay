# Rozmiar kodu ABI v2 na nes

Segment `CODE` w bajtach po linkowaniu z biblioteką standardową (`cathode cc --ir vreg [--abi v2]`); osobny golden pilota v2. Bramka -15% na wołaniowych (plan 39, zadanie 3) NIE przeszła: 05_calls 349→346 (-0.9%), średnia +1.7 B (8 mniejszych, 1 równy, 15 większych, max +23). Optymalizacje w cenie: ParamAlias pamięciowy na v2, stopka z jednym parkiem A, strażnik kolizji pha/pla, stdlib C zawsze v1. Strukturalny sufit: tylko arg0 w rejestrach (D1), helpery v1 (round-trip cc_arg/cc_ret), writeback crt0; dalsze w itemie 5 (ParamAlias rejestrowy).

| program | v1 | v2 | delta |
| --- | ---: | ---: | ---: |
| 01_types.c | 236 | 240 | +4 |
| 02_control.c | 302 | 305 | +3 |
| 04_int_ops.c | 609 | 610 | +1 |
| 05_calls.c | 349 | 346 | -3 |
| 06_defines.c | 179 | 180 | +1 |
| 07_control.c | 386 | 380 | -6 |
| 08_int_signed.c | 954 | 955 | +1 |
| 09_init.c | 801 | 802 | +1 |
| 10_struct.c | 413 | 408 | -5 |
| 11_goto_typedef.c | 506 | 496 | -10 |
| 12_globals_init.c | 579 | 575 | -4 |
| 13_strings.c | 2224 | 2224 | +0 |
| 14_macros.c | 131 | 134 | +3 |
| 15_funcptr.c | 766 | 790 | +24 |
| 16_printf.c | 3958 | 3961 | +3 |
| 17_casts.c | 331 | 330 | -1 |
| 18_voidptr.c | 857 | 858 | +1 |
| 19_struct_value.c | 2397 | 2418 | +21 |
| 20_matrix.c | 1280 | 1296 | +16 |
| 21_long.c | 5765 | 5754 | -11 |
| 22_misc.c | 1019 | 1020 | +1 |
| 23_types.c | 1883 | 1884 | +1 |
| 24_float.c | 15355 | 15346 | -9 |
| 25_longlong.c | 11399 | 11410 | +11 |
