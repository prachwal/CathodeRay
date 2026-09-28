; Zakresy dla 65C02 (ca65): STZ/BRA w procedurach z kolizją loop.
        .org $0800
.proc foo
loop:   stz $20
        bra loop
.endproc
.proc bar
loop:   stz $21
        bra loop
.endproc
        lda foo::loop
        lda bar::loop
        rts
