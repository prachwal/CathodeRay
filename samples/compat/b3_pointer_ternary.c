// B3: operator ?: ze wskaźnikami — wskaźnik i 0/NULL, tablica i wskaźnik, void* z innym wskaźnikiem, wynik jako argument,
// wartość zwracana i łańcuch zagnieżdżonych ?:.
// expect-result: 103
#include <stddef.h>

int table[3] = {10, 20, 30};
int other[3] = {1, 2, 3};
int value;

int dbl2(int x) { return x * 2; }
int inc1(int x) { return x + 1; }

int *pick(int which)
{
    return which == 1 ? table : which == 2 ? other : NULL;
}

int read(int *p)
{
    return p ? p[1] : -1;
}

void *erased(int flag)
{
    return flag ? (void *)table : NULL;
}

int main(void)
{
    int total = 0;
    int *p = value ? &value : 0;
    int *q = value == 0 ? table : other;
    void *v = 1 ? (void *)q : (void *)p;
    int *r = (int *)(0 ? v : erased(1));

    total += p == 0;                      /* 1 */
    total += q[0];                        /* 10 */
    total += read(pick(1)) + read(pick(2)) + read(pick(3));     /* 20 + 2 - 1 */
    total += read(value ? table : NULL);  /* -1 */
    total += r[2];                        /* 30 */
    total += *(int *)v;                   /* 10 */
    {
        const int *cp = value ? table : (const int *)other;     /* int* i const int* => const int* */
        int (*fp)(int) = value ? inc1 : dbl2;                    /* dwa wskaźniki do funkcji */
        int *z1 = value ? table : 0U;                            /* 0U, (1 - 1) to stałe zerowe */
        int *z2 = value ? (1 - 1) : table;

        total += cp[0];                       /* 1 */
        total += fp(10);                      /* 20 */
        total += z1 == 0;                     /* 1 */
        total += z2[0];                       /* 10 */
    }
    return total;
}
