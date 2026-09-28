; Przykład podprogramu dla ISA zaślepki: CALL/RET ze stosem na stronie 01xxh.
; cathode asm samples/stub/subroutine.asm --cpu stub
; cathode stub run samples/stub/subroutine.bin --dump 0x20:2

start:  LDI 21
        CALL add21
        STA result
        HLT

add21:  ADD 21
        RET

        .org $0020
result: .byte 0
