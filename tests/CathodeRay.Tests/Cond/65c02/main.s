; Warunkowe dla 65C02 (ca65): STZ/BRA tylko w aktywnej gałęzi.
debug   = 1
ptr     = $20

        .org $0800
        .if debug = 1
        stz ptr
        .else
        lda ptr
        .endif
        .if debug = 0
        nop
        .elseif debug = 1
        bra done
        lda #$FF
done:   rts
        .endif
