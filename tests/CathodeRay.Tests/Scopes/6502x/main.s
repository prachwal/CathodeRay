; Zakresy dla 6502X (ca65): LAX w procedurach z kolizją loop.
        .org $0600
.proc foo
loop:   lax $12
        bpl loop
        rts
.endproc
.proc bar
loop:   lax $13
        bpl loop
        rts
.endproc
        lda foo::loop
        lda bar::loop
        rts
