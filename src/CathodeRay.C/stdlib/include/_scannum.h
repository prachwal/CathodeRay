/* _scannum.h — wspólny skaner liczb dla sscanf i strtod (wewnętrzny: dołączany do modułu). scan_number czyta liczbę dziesiętną
   (znak, cyfry, przy real: ułamek i wykładnik e/E) do float; zwraca 0, gdy nie ma cyfr, i przesuwa *sp za przeczytany tekst. */
#include <ctype.h>

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
