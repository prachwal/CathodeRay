; 6502X: LAX w makrze z .local.
        .org $0600
        .macro get zp
        .local done
        lax zp
        bpl done
        lda #0
done:
        .endmacro
        get $12
        get $13
        rts
