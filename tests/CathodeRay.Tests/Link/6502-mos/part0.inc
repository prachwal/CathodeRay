COUNT   = 8

        *= $0600
START   LDX #COUNT
        LDA #<MESSAGE
        STA PTR
        LDA #>MESSAGE
        STA PTR+1
