// B2: zakresy blokowe — te same nazwy w blokach obok siebie, zacienianie zmiennej zewnętrznej, parametru i globalnej,
// `for (int i...)` kilka razy, osobne zmienne static w blokach, adres zmiennej wewnętrznej i lokalny wskaźnik do funkcji
// o nazwie zmiennej z innego bloku.
// expect-result: 1195
int x = 100;

int twice(int v) { return v * 2; }

int bump(int *p) { *p += 1; return *p; }

void *self_g;

/* deklaracja bezpośrednio pod case ma własny zakres */
int switch_scope(void)
{
    int x = 7;
    switch (0) {
    case 0:
        int x = 3;
        x += 1;
        break;
    }
    return x;                                     /* 7 */
}

/* inicjalizator widzi własną, zacieniającą zmienną: `void *self_g = &self_g` */
int self_reference(void)
{
    void *self_g = &self_g;
    return self_g == (void *)&self_g;             /* 1 */
}

/* lokalna o nazwie późniejszej globalnej nie przeszkadza jej użyciu w innej funkcji */
int late_local(void)
{
    int late = 3;
    return late;
}

int late = 5;

int late_global(void)
{
    return late;                                  /* 5 */
}

int count_calls(void)
{
    { static int n; n += 1; if (n > 100) return -1; }
    { static int n; n += 10; return n; }
}

int main(void)
{
    int total = 0;
    int t = 5;
    uchar i;

    { int a = 1; total += a; }
    { int a = 2; total += a; }

    {
        int a = 10;
        total += a;
        {
            int a = 20;
            total += a;
            a += 1;
            total += a;
        }
        total += a;
    }

    total += x;
    {
        int x = 7;                                /* zacienia globalną */
        total += x;
    }
    total += x;

    {
        int t = 1;                                /* zacienia zmienną z main */
        total += t;
    }
    total += t;

    for (int i = 0; i < 3; i++) {
        total += i;
    }
    for (int i = 10; i < 12; i++) {
        total += i;
    }
    for (i = 0; i < 2; i++) {
        int sq = i * i;
        total += sq;
    }
    for (i = 0; i < 2; i++) {
        int sq = i + 100;
        total += sq;
    }

    {
        int v = 41;
        total += bump(&v);
    }
    {
        int v = 5;
        total += bump(&v);
    }

    count_calls();
    total += count_calls();                       /* statyczne n: 2 + 20 = 20 -> 570 */

    {
        int (*twice)(int *) = 0;                    /* lokalna o nazwie funkcji: osobny zakres */
        twice = bump;
        {
            int w = 3;
            total += twice(&w);
        }
    }
    total += twice(3);                            /* funkcja globalna: 6 -> 580 */
    total += switch_scope() + self_reference() + late_local() * 0 + late_global();
    return total + 601;
}
