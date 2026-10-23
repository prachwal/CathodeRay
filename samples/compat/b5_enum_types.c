// B5: enum jako typ — pole struktury z wbudowanym enum, zmienna `enum Nazwa x`, typedef enum, tablica anonimowych struktur
// z wskaźnikami do funkcji i enumem (tablica prymitywów interpretera), stałe enum w wyrażeniach.
// expect-result: 1001
typedef enum { OFF, ON = 5, AUTO } Mode;
enum Color { RED, GREEN = 10, BLUE };

int inc(int x) { return x + 1; }
int dbl(int x) { return x * 2; }
int neg(int x) { return -x; }

const struct {
    const char *name;
    int (*fn)(int);
    enum { NORMAL, SPECIAL, TAILCALL } kind;
} prim[] = {
    {"inc", inc, NORMAL},
    {"dbl", dbl, SPECIAL},
    {"neg", neg, NORMAL | TAILCALL},
};

struct device {
    enum Color color;
    Mode mode;
};

int main(void)
{
    struct device d;
    enum Color c = BLUE;
    Mode m = AUTO;
    int total = 0;
    uchar i;

    d.color = GREEN;
    d.mode = ON;
    total += d.color + d.mode;                    /* 15 */
    total += c + m;                               /* 11 + 6 = 17 */
    for (i = 0; i < 3; i++) {
        if (prim[i].kind & TAILCALL) {
            total += prim[i].fn(100) + 1000;      /* -100 + 1000 */
        } else if (prim[i].kind == SPECIAL) {
            total += prim[i].fn(20);              /* 40 */
        } else {
            total += prim[i].fn(7);               /* 8 */
        }
    }
    total += (enum Color)GREEN + (Mode)ON;        /* 15 */
    return total + sizeof(prim[0].kind) + sizeof(struct device);
}
