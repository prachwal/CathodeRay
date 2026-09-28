; z80u: IXH w makrze z LOCAL.
GET:    MACRO zp
        LOCAL done
        LD A,IXH
        OR A
        JR Z,done
        LD A,IXL
done:
        ENDM
        ORG 8000H
        GET 1
        GET 2
        RET
