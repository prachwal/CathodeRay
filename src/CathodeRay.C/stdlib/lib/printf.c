/* printf/sprintf: dodatkowe argumenty to 16-bit w cc_arg2..cc_arg6, więc definicja ma stałą liczbę parametrów,
   a nagłówek deklaruje wersję wariadyczną. */
#include <stdio.h>

static uchar *dst;
static uint written;

static void out(uchar c) {
    if (dst) {
        *dst = c;
        dst++;
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
        int v = next < 5 ? args[next] : 0;
        next++;
        switch (c) {
            case 'd':
                if (v < 0) outnum(0 - v, 10, 1); else outnum(v, 10, 0);
                break;
            case 'u':
                outnum(v, 10, 0);
                break;
            case 'x':
                outnum(v, 16, 0);
                break;
            case 'c':
                out(v);
                break;
            case 's':
                outstr((const uchar *)v);
                break;
            default:
                out('%');
                out(c);
                next--;
        }
    }
    return written;
}

int printf(const uchar *fmt, int a1, int a2, int a3, int a4, int a5) {
    dst = 0;
    written = 0;
    return format(fmt, a1, a2, a3, a4, a5);
}

int sprintf(uchar *buf, const uchar *fmt, int a1, int a2, int a3, int a4) {
    dst = buf;
    written = 0;
    format(fmt, a1, a2, a3, a4, 0);
    *dst = 0;
    dst = 0;
    return written;
}
