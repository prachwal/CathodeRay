; Ciąg Fibonacciego F0..F24 (16-bit, F24 = 46368): młodsze bajty w lo, starsze w hi.
; cathode stub asm samples/stub/fib16.asm
; cathode stub run samples/stub/fib16.bin --dump 0x200:25 --dump 0x300:25
;
; Młodszy bajt przez ADD (ustawia C), starszy przez ADC (dodaje C) — bez CLC, bo ADD nie bierze C na wejściu.

        LDX 2
loop:   LDA lo-2,X
        ADD lo-1,X
        STA lo,X
        LDA hi-2,X
        ADC hi-1,X
        STA hi,X
        INX
        CPX 25
        BNE loop
        HLT

        .org $0200
lo:     .byte 0, 1

        .org $0300
hi:     .byte 0, 0
