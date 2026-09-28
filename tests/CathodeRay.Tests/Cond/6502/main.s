; Warunkowe porównywane z ca65: wybór wariantu, elseif, zagnieżdżenie,
; symbole tylko z aktywnej gałęzi, porównania w wyrażeniach.
debug   = 1
platform = 2

        .org $0600
start:  .if debug = 1
        ldx #1
        .elseif platform = 2
        ldx #2
        .else
        ldx #3
        .endif
        .if debug <> 0
        lda #<active
        .endif
        .if platform > 1
        .if debug <> 0
        lda #>active
        .endif
        .endif
        .if platform <= 1
        lda #$FF
        .else
        sta active
        .endif
        rts
active: .byte $42
