; Z80: lokalne i INCBIN, porównane z z80asm -mz80_strict.
        ORG 8000H
aa:     LD A,1
@x:     LD A,2
bb:     LD A,3
@x:     LD A,4
        LD A,(@x)
        JR @fwd
@fwd:   INCBIN "data.bin"
        RET
