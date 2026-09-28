; z80u: lokalne i INCBIN (IXL w zakresie).
        ORG 8000H
aa:     LD A,IXL
@x:     NOP
bb:     LD A,IXH
@x:     NOP
        LD A,(@x)
        INCBIN "data.bin"
        RET
