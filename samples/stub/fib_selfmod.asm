; Wersja bez X/BNE/trybu indeksowego (porównanie z fib.asm). Ciąg Fibonacciego F0..F13 (8-bit, F13 = 233) do tablicy fib ($0200..$020D).
; cathode stub asm samples/stub/fib_selfmod.asm
; cathode stub run samples/stub/fib_selfmod.bin --trace --dump 0x200:14
;
; ISA stuba nie ma skoku warunkowego ani ADD z pamięci, więc:
; - a + b:    kod samomodyfikujący (STA łata operand ADD),
; - fib[idx]: łatany młodszy bajt adresu STA (fib leży na granicy strony),
; - koniec:   skok przez tablicę skoków; disp += 3 wskazuje kolejny wpis,
;             wpisy 1..11 wracają do loop, wpis 12 to HLT.

loop:   LDA curr
        STA add_op+1        ; operand ADD := curr
        LDA prev
add_op: ADD 0               ; A = prev + curr
        STA next
        LDA curr
        STA prev            ; prev := curr
        LDA next
        STA curr            ; curr := next
        LDA idx
        STA store+1         ; młodszy bajt adresu := idx
        LDA next
store:  STA fib             ; fib[idx] := next
        LDA idx
        INC
        STA idx
        LDA disp
        ADD 3
        STA disp
        STA jump+1          ; młodszy bajt adresu wpisu tablicy skoków
jump:   JMP table

prev:   .byte 0             ; F(k-1)
curr:   .byte 1             ; F(k)
next:   .byte 0
idx:    .byte 2             ; kolejny indeks w fib
disp:   .byte 0             ; 3 * numer iteracji

        .org $0100
table:  .byte 0, 0, 0       ; wpis 0: nieużywany
        JMP loop            ; 1  -> F2 gotowe
        JMP loop            ; 2
        JMP loop            ; 3
        JMP loop            ; 4
        JMP loop            ; 5
        JMP loop            ; 6
        JMP loop            ; 7
        JMP loop            ; 8
        JMP loop            ; 9
        JMP loop            ; 10
        JMP loop            ; 11
        HLT                 ; 12 -> F13 gotowe

        .org $0200
fib:    .byte 0, 1
