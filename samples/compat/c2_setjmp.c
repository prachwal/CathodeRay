// C2: <setjmp.h> — setjmp/longjmp: powrót z głębokich wywołań, przekazanie wartości, longjmp(env, 0) daje 1,
// lokalne zmienne w pamięci przeżywają, wielokrotne rzucanie w pętli (wzorzec obsługi błędów z interpreterów).
// expect-result: 2404
#include <setjmp.h>

jmp_buf env;
int depth_reached;

void descend(int n, int code)
{
    depth_reached = n;
    if (n == 0) {
        longjmp(env, code);
    }
    descend(n - 1, code);
}

int attempt(int levels, int code)
{
    int r = setjmp(env);
    if (r == 0) {
        descend(levels, code);
        return -1;          /* nieosiągalne */
    }
    return r;
}

/* Zmienne trzymane normalnie w rejestrach (z80/8080) muszą przeżyć longjmp: funkcja z setjmp ich nie dostaje. */
int keeps_locals(void)
{
    uchar a = 5;
    uchar b = 2;
    uchar r;

    a = a * 3;
    r = setjmp(env);
    if (r == 0) {
        a += 10;
        b += a;
        descend(4, 1);
    }
    return a + b + r;                    /* 25 + 27 + 1 */
}

int main(void)
{
    int total = 0;
    int saved = 1000;
    int i;
    int r;

    total += keeps_locals();             /* 53 */
    total += attempt(3, 7);              /* 7 */
    total += attempt(5, 0);              /* longjmp(env, 0) => 1 */
    total += depth_reached;              /* 0 po zejściu do n == 0 */

    r = setjmp(env);
    if (r == 0) {
        saved = 2000;                    /* zapis po setjmp: lokalne w pamięci zachowują wartość */
        longjmp(env, 40);
    }
    total += saved + r;                  /* 2000 + 40 */

    for (i = 0; i < 3; i++) {
        r = setjmp(env);
        if (r == 0) {
            longjmp(env, 100 + i);
        }
        total += r;                      /* 100 + 101 + 102 */
    }
    return total;
}
