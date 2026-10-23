// B2: zakresy blokowe — te same nazwy w blokach obok siebie, zacienianie zmiennej zewnętrznej, parametru i globalnej,
// `for (int i...)` kilka razy, osobne zmienne static w blokach, adres zmiennej wewnętrznej i lokalny wskaźnik do funkcji
// o nazwie zmiennej z innego bloku.
// expect-result: 1182
int x = 100;

int twice(int v) { return v * 2; }

int bump(int *p) { *p += 1; return *p; }

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
    return total + 601;
}
