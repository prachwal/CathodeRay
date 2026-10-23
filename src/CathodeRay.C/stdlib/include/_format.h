/* _format.h — wspólny formatter printf/sprintf/snprintf (wewnętrzny: dołączany do modułu, nie do programu użytkownika).
   Dodatkowe argumenty to 16-bit w cc_arg2..cc_arg6, więc format() ma stałą liczbę parametrów. FMT_LIMIT włącza ograniczenie liczby
   zapisanych znaków (snprintf); bez niego kod jest bajt w bajt taki jak dotąd, więc programy z samym printf nie rosną. */
#include <stdio.h>
#ifdef FMT_FLOAT
#include <stdlib.h>
#endif


static uchar *dst;
static uint written;
#ifdef FMT_LIMIT
static uint limit;
#endif

static void out(uchar c) {
    if (dst) {
#ifdef FMT_LIMIT
        if (written < limit) {
            *dst = c;
            dst++;
        }
#else
        *dst = c;
        dst++;
#endif
    } else {
        putchar(c);
    }
    written++;
}

static void outstr(const uchar *s) {
    while (*s) {
        out(*s);
        s++;
    }
}

static void outnum(uint u, uchar base, uchar negative) {
    uchar tmp[17];
    uchar n = 0;
    if (negative) out('-');
    if (u == 0) {
        tmp[n] = '0';
        n++;
    }
    while (u > 0) {
        uchar d = u % base;
        tmp[n] = d < 10 ? '0' + d : 'a' + d - 10;
        n++;
        u = u / base;
    }
    while (n > 0) {
        n--;
        out(tmp[n]);
    }
}

static void outnum32(ulong u, uchar base, uchar negative) {
    uchar tmp[33];
    uchar n = 0;
    if (negative) out('-');
    if (u == 0) {
        tmp[n] = '0';
        n++;
    }
    while (u > 0) {
        uchar d = u % base;
        tmp[n] = d < 10 ? '0' + d : 'a' + d - 10;
        n++;
        u = u / base;
    }
    while (n > 0) {
        n--;
        out(tmp[n]);
    }
}

static int format(const uchar *fmt, int a1, int a2, int a3, int a4, int a5) {
    int args[5];
    args[0] = a1;
    args[1] = a2;
    args[2] = a3;
    args[3] = a4;
    args[4] = a5;
    uchar next = 0;
    while (*fmt) {
        uchar c = *fmt;
        fmt++;
        if (c != '%') {
            out(c);
            continue;
        }
        c = *fmt;
        if (c == 0) break;
        fmt++;
        if (c == '%') {
            out('%');
            continue;
        }
#ifdef FMT_FLOAT
        /* flagi, szerokość i precyzja (%.17lg) są pomijane */
        while (c == '-' || c == '+' || c == ' ' || c == '#' || c == '.' || (c >= '0' && c <= '9')) {
            c = *fmt;
            if (c == 0) break;
            fmt++;
        }
        if (c == 0) break;
#endif
        uchar wide = 0;
        if (c == 'l') {
            wide = 1;
            c = *fmt;
            if (c == 0) break;
            fmt++;
        }
#ifdef FMT_FLOAT
        if (c == 'f' || c == 'g' || c == 'e') wide = 1;
#endif
        int v = next < 5 ? args[next] : 0;
        next++;
        ulong big = (ulong)(uint)v;
        if (wide) {
            uint high = next < 5 ? args[next] : 0;
            big = big | ((ulong)high << 16);
            next++;
        }
        switch (c) {
            case 'd':
                if (wide) {
                    if (big & 0x80000000) outnum32(0 - big, 10, 1); else outnum32(big, 10, 0);
                } else if (v < 0) outnum(0 - v, 10, 1); else outnum(v, 10, 0);
                break;
            case 'u':
                if (wide) outnum32(big, 10, 0); else outnum(v, 10, 0);
                break;
            case 'x':
                if (wide) outnum32(big, 16, 0); else outnum(v, 16, 0);
                break;
            case 'c':
                out(v);
                break;
#ifdef FMT_FLOAT
            case 'f':
            case 'g':
            case 'e': {
                uchar tmp[24];
                uchar n = 0;
                uchar i;
                uchar dot = 0;
                ftoa(*(float *)&big, tmp);
                while (tmp[n]) n++;
                for (i = 0; i < n; i++) {
                    if (tmp[i] == '.') dot = 1;
                }
                /* %g i %e: bez końcowych zer ułamka (ftoa zawsze daje 6 cyfr) */
                if (c != 'f' && dot) {
                    while (tmp[n - 1] == '0') n--;
                    if (tmp[n - 1] == '.') n--;
                }
                for (i = 0; i < n; i++) out(tmp[i]);
                break;
            }
#endif
            case 's':
                outstr((const uchar *)v);
                break;
            default:
                out('%');
                out(c);
                next--;
                if (wide) next--;
        }
    }
    return written;
}

