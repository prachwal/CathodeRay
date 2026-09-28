count   = 8

        .org $0600
start:  ldx #count
        lda #<message
        sta ptr
        lda #>message
        sta ptr+1
