; Zakresy porównywane z ca65: .proc z kolizją loop, dostęp foo::loop,
; .scope bez etykiety, zagnieżdżenie outer::inner::val, anonimowy .scope.
        .org $0600
.proc foo
loop:   lda #1
        bne loop
        rts
.endproc
.proc bar
loop:   lda #2
        bne loop
        rts
.endproc
.scope glob
shared: nop
.endscope
.scope outer
.scope inner
val:    nop
.endscope
.endscope
.scope
hidden: nop
.endscope
        lda foo::loop
        lda bar::loop
        lda glob::shared
        lda outer::inner::val
        rts
