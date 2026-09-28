; Tanie etykiety i .incbin, porównane z ca65: zakresy, forward-ref w zakresie.
        .org $0600
aa:     lda #1
@x:     lda #2
bb:     lda #3
@x:     lda #4
        lda @x
        jmp @fwd
@fwd:   .incbin "data.bin"
        rts
