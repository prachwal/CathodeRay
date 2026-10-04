; Small executable reference for the 6809 emulator.
	LDS #$0FFF
	LDX #$2000
	LDA #$2A
	STA ,X
	LDA #0
	LDA ,X
	CMPA #$2A
	BNE fail
	BRA done
fail: 	LDA #$FF
done: 	SYNC
