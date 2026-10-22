#include <ctype.h>
#include <stdio.h>

/* sscanf: podzbiór — %d, %g/%f/%e, %n i spacje; do 3 wskaźników (kolejne konwersje kończą skanowanie). Zwraca liczbę przypisanych
   pól, a EOF, gdy wejście skończyło się przed pierwszą konwersją (także przy literale formatu), jak w C. Wskaźniki to 16-bit
   argumenty wariadyczne. Wynik to float; z -D CC_DOUBLE64 specyfikatory z `l` (%lg, %lf, %le) zapisują 8-bajtowy double. */
#ifdef CC_DOUBLE64
void __cc_ftod(ulong a, unsigned long long *out);
#endif

static int scan_number(const uchar **sp, float *out, uchar real) {
    const uchar *s = *sp;
    float v = 0;
    float scale = 1;
    uchar neg = 0;
    uchar digits = 0;
    int ex = 0;
    if (*s == '-') {
        neg = 1;
        s++;
    } else if (*s == '+') {
        s++;
    }
    while (isdigit(*s)) {
        v = v * 10 + (*s - '0');
        digits = 1;
        s++;
    }
    if (real && *s == '.') {
        s++;
        while (isdigit(*s)) {
            v = v * 10 + (*s - '0');
            scale = scale * 10;
            digits = 1;
            s++;
        }
    }
    if (!digits) return 0;
    if (real && (*s == 'e' || *s == 'E')) {
        const uchar *t = s + 1;
        uchar eneg = 0;
        uchar edigits = 0;
        int e = 0;
        if (*t == '-') {
            eneg = 1;
            t++;
        } else if (*t == '+') {
            t++;
        }
        while (isdigit(*t)) {
            e = e * 10 + (*t - '0');
            edigits = 1;
            t++;
        }
        if (edigits) {
            ex = eneg ? 0 - e : e;
            s = t;
        }
    }
    v = v / scale;
    /* poza zakresem float wynik to inf albo 0, więc dalsze kroki nic nie zmienią */
    if (ex > 64) ex = 64;
    if (ex < -64) ex = -64;
    while (ex > 0) {
        v = v * 10;
        ex--;
    }
    while (ex < 0) {
        v = v / 10;
        ex++;
    }
    *out = neg ? 0 - v : v;
    *sp = s;
    return 1;
}

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
