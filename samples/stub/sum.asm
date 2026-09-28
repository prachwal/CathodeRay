; Przykład dla ISA zaślepki: 250 + 10 przepełnia bajt (C=1), wynik trafia pod result.
; cathode stub asm samples/stub/sum.asm
; cathode stub run samples/stub/sum.bin --trace --dump $0020:4

start:  LDA operand     ; A = 250
        ADD 10          ; A = 4, C = 1
        STA result
        INC             ; flagi bez zmian
        JMP done
        NOP             ; pomijane
done:   HLT

        .org $0020
result: .byte 0
operand: .byte 250
