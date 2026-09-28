        .org $0800
start:  stz ptr
        stz $1234,x
        lda (ptr)
        sta (ptr)
        bra skip
