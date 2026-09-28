        ldy #0
loop:   lda (ptr),y
        beq done
        sta screen,y
        iny
        bne loop
done:   jmp finish          ; odwołanie w przód
        lda later           ; stała zdefiniowana później: forma absolute, jak w ca65
