; Instrukcje i tryby specyficzne dla 65C02, porównywane z ca65 --cpu 65C02.
ptr     = $20

        .org $0800
start:  stz ptr
        stz $1234,x
        lda (ptr)
        sta (ptr)
        bra skip
        jmp (table,x)
skip:   bbr0 ptr, start
        bbs7 ptr, done
        rmb3 ptr
        smb5 ptr
        tsb $1234
        trb ptr
        inc a
        dec
        phx
        ply
done:   rts
table:  .word start, skip, done
