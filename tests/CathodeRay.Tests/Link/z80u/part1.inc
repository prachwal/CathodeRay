        RLC (IX+1)
        BIT 3+4,(IX-1)
        SET 0,(IY+127)
        RES 7,(HL)
        OUT (port),A
        IN A,(C)
        JR NZ,done
        JP (IX)
        EX AF,AF'           ; zamiana z rejestrami alternatywnymi
done:   IM 2
