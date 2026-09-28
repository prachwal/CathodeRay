; Makra porównane z ca65: parametr, .local, makro wołające makro.
        .org $0600
        .macro add2 val
        lda #val
        clc
        adc #val
        .endmacro
        .macro load1 val
        .local done
        lda #val
        ldx #val
done:
        .endmacro
        .macro twice v
        add2 v
        add2 v
        .endmacro
start:  add2 5
        load1 7
        twice 3
        rts
