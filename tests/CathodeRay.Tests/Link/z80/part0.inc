        ORG 8000H
start:  LD IX,table
        LD B,count
        XOR A
loop:   ADD A,(IX+0)
        ADD A,(IX)
        INC IX
        DJNZ loop
        LD (IX-2),A
