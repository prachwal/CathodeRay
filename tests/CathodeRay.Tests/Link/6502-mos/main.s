; To samo co program.s w składni MOS: etykiety bez dwukropka od kolumny 1, *=, .BYTE, .WORD.
; Test oczekuje binarki identycznej z program.bin (wygenerowanej przez ca65 z program.s).
PTR     = $10
SCREEN  = $0400
.INCLUDE "part0.inc"
.INCLUDE "part1.inc"
        DEX
        BPL START
FINISH  RTS
MESSAGE .BYTE "HELLO", 0
TABLE   .WORD START, FINISH, TABLE
        .BYTE <TABLE, >TABLE, COUNT*2+1, %1010, 'A'
        .BYTE $EA, $EA, $EA
LATER   = $20
