; Program porównywany z as6800: stałe equ, etykiety, odwołania w przód,
; strona zerowa kontra rozszerzony, skoki względne, wyrażenia, dyrektywy danych.
ptr     equ $10
screen  equ $0400
count   equ 8
later   equ $20           ; zdefiniowane przed użyciem: as6800 robi więcej
                          ; przebiegów i też wybierze direct (my: odwołanie
                          ; w przód = najdłuższa forma, jak ca65)

        org $0600
start:  ldx #count
        ldaa #<message
        staa ptr
        ldaa #>message
        staa ptr+1
        ldx #0
loop:   ldaa 0,x
        beq done
        staa 1,x
        inx
        bra loop
done:   jmp finish
        ldaa later
        dex
        bpl start
finish: rts
message: .ascii "HELLO"
        .byte 0
table:  .word start
        .word finish
        .word table
        .byte count*2+1
        .blkb 3
