; 65C02: lokalne + .incbin (STZ w zakresie).
        .org $0800
aa:     stz $20
@x:     nop
bb:     stz $21
@x:     nop
        lda @x
        .incbin "data.bin"
        rts
