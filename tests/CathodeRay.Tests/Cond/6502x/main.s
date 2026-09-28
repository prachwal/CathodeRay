; Warunkowe dla 6502X (ca65): nieudokumentowany LAX tylko w aktywnej gałęzi.
debug   = 0

        .org $0600
        .if debug = 1
        lax $12
        .else
        lda $12
        .endif
        .if debug <> 0
        nop
        .endif
        rts
