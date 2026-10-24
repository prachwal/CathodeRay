// B4: switch na wartości 32-bitowej (unsigned long / long) — tag NaN-boxingu lub kod rozkazu; literały case w pełnym zakresie 32 bitów,
// stałe ujemne (case -1) rozszerzane ze znakiem.
// expect-result: 4991
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

int wide_cases(unsigned long v)
{
    switch (v) {
    case 0x10000UL: return 100;
    case 0x80000000UL: return 200;
    case 0xFFFFFFFFUL: return 300;
    default: return 0;
    }
}

int negative_cases(long v)
{
    switch (v) {
    case -1: return 10;
    case -32768: return 20;
    case 32767: return 30;
    default: return 1;
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
    total += wide_cases(0x10000UL) + wide_cases(0x80000000UL) + wide_cases(0xFFFFFFFFUL) + wide_cases(0);   /* 600 */
    total += negative_cases(-1L) + negative_cases(-32768L) + negative_cases(32767L) + negative_cases(65535L);  /* 10 + 20 + 30 + 1 */
    return total;
}
