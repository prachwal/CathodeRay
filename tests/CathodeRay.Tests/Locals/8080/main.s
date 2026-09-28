; 8080: lokalne i INCBIN, porównane z z80asm -m8080.
        ORG 100H
aa:     MVI A,1
@x:     MVI A,2
bb:     MVI A,3
@x:     MVI A,4
        LDA @x
        INCBIN "data.bin"
        HLT
