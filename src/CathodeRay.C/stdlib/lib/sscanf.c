#include <stdio.h>
#include <_scannum.h>

/* sscanf: podzbiór — %d, %g/%f/%e, %n i spacje; do 3 wskaźników (kolejne konwersje kończą skanowanie). Zwraca liczbę przypisanych
   pól, a EOF, gdy wejście skończyło się przed pierwszą konwersją (także przy literale formatu), jak w C. Wskaźniki to 16-bit
   argumenty wariadyczne. Wynik to float; z -D CC_DOUBLE64 specyfikatory z `l` (%lg, %lf, %le) zapisują 8-bajtowy double. */
#ifdef CC_DOUBLE64
void __cc_ftod(ulong a, unsigned long long *out);
#endif

int sscanf(const uchar *str, const uchar *fmt, int a1, int a2, int a3) {
    int args[3];
    uchar next = 0;
    uchar wide;
    int count = 0;
    const uchar *s = str;
    args[0] = a1;
    args[1] = a2;
    args[2] = a3;
    while (*fmt) {
        uchar c = *fmt;
        fmt++;
        if (isspace(c)) {
            while (isspace(*s)) s++;
            continue;
        }
        if (c != '%') {
            if (*s == 0 && count == 0) return EOF;
            if (*s != c) break;
            s++;
            continue;
        }
        c = *fmt;
        wide = 0;
        if (c == 'l') {
            wide = 1;
            fmt++;
            c = *fmt;
        }
        if (c == 0) break;
        fmt++;
        if (next >= 3) break;
        if (c == 'n') {
            *(int *)args[next] = s - str;
            next++;
            continue;
        }
        while (isspace(*s)) s++;
        if (*s == 0) return count == 0 ? EOF : count;
        float f;
        if (c == 'd') {
            if (!scan_number(&s, &f, 0)) break;
            *(int *)args[next] = (int)f;
        } else if (c == 'g' || c == 'f' || c == 'e') {
            if (!scan_number(&s, &f, 1)) break;
#ifdef CC_DOUBLE64
            if (wide) {
                __cc_ftod(*(ulong *)&f, (unsigned long long *)args[next]);
            } else {
                *(float *)args[next] = f;
            }
#else
            *(float *)args[next] = f;
#endif
        } else {
            break;
        }
        next++;
        count++;
    }
    return count;
}
