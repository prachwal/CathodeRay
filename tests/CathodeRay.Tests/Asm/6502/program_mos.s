; To samo co program.s w składni MOS: etykiety bez dwukropka od kolumny 1, *=, .BYTE, .WORD.
; Test oczekuje binarki identycznej z program.bin (wygenerowanej przez ca65 z program.s).
PTR     = $10
SCREEN  = $0400
COUNT   = 8

        *= $0600
START   LDX #COUNT
        LDA #<MESSAGE
        STA PTR
        LDA #>MESSAGE
        STA PTR+1
        LDY #0
LOOP    LDA (PTR),Y
        BEQ DONE
        STA SCREEN,Y
        INY
        BNE LOOP
DONE    JMP FINISH
        LDA LATER
        DEX
        BPL START
FINISH  RTS
MESSAGE .BYTE "HELLO", 0
TABLE   .WORD START, FINISH, TABLE
        .BYTE <TABLE, >TABLE, COUNT*2+1, %1010, 'A'
        .BYTE $EA, $EA, $EA
LATER   = $20
