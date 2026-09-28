; 65C02: STZ/BRA w makrze.
        .org $0800
        .macro zero ptr
        .local done
        stz ptr
        bra done
        lda #$FF
done:
        .endmacro
        zero $20
        rts
