; Ciąg Fibonacciego F0..F13 (8-bit, F13 = 233) do tablicy fib ($0200..$020D).
; cathode asm samples/stub/fib.asm --cpu stub
; cathode stub run samples/stub/fib.bin --trace --dump 0x200:14
;
; fib[X] = fib[X-2] + fib[X-1] dla X = 2..13; wersja bez tych instrukcji: fib_selfmod.asm.

        LDX 2
loop:   LDA fib-2,X         ; F(X-2)
        ADD fib-1,X         ; + F(X-1)
        STA fib,X           ; F(X)
        INX
        CPX 14
        BNE loop
        HLT

        .org $0200
fib:    .byte 0, 1
