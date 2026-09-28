; 6502X: lokalne + .incbin (LAX w drugiej gałęzi zakresu).
        .org $0600
aa:     lax $12
@x:     nop
bb:     lax $13
@x:     nop
        lda @x
        .incbin "data.bin"
        rts
