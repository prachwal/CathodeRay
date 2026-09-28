        LDY #0
LOOP    LDA (PTR),Y
        BEQ DONE
        STA SCREEN,Y
        INY
        BNE LOOP
DONE    JMP FINISH
        LDA LATER
