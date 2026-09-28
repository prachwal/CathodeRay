; Long-tail porównywane z ca65: .out/.warning/.assert/.define/.ifnblank.
.define STEP 2
.define MSG "hi"
        .org $0600
        .out MSG
        .warning "golden"
        .assert STEP = 2, error, "step"
        .assert STEP = 3, warning, "silent"
        .macro one v
        .ifnblank v
        lda #v
        .endif
        .endmacro
        one STEP
        one
        .macro cnt v
        lda #.paramcount
        .endmacro
        cnt STEP
        rts
