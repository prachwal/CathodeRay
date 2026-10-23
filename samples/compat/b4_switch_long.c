// B4: switch na wartości 32-bitowej (unsigned long / long) — typowy tag NaN-boxingu lub kod rozkazu; stałe case 0..65535.
// expect-result: 4330
typedef unsigned long I;

enum { PRIM = 0x7ff9, ATOM = 0x7ffa, CONS = 0x7ffc, NIL = 0xffff };

I tag_of(I v) { return v >> 8; }

int classify(I tag)
{
    switch (tag) {
    case PRIM: return 1;
    case ATOM: return 20;
    case CONS: return 300;
    case NIL: return 4000;
    default: return 5;
    }
}

int sign_of(long v)
{
    switch (v) {
    case 0: return 7;
    case 1: return 8;
    default: return 9;
    }
}

int main(void)
{
    I big = 0x7ffc00UL + 0x34;              /* tag_of => 0x7ffc */
    int total = 0;
    total += classify(tag_of(big));                      /* 300 */
    total += classify(0x7ff9UL);                         /* 1 */
    total += classify(0x10000UL + 0x7ffaUL);             /* 5: wartość 32-bitowa nie pasuje do ATOM */
    total += classify(0xffffUL);                         /* 4000 */
    total += sign_of(0) + sign_of(1) + sign_of(100000L); /* 7 + 8 + 9 */
    return total;
}
