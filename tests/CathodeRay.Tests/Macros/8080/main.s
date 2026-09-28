; 8080: MACRO/ENDM/LOCAL w składni Intela.
PUT:    MACRO v
        LOCAL done
        MVI A,v
        ORA A
        JZ done
        MVI B,v
done:
        ENDM
        ORG 100H
        PUT 9
        PUT 0
        HLT
