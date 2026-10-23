#include <stdlib.h>
#include <_scannum.h>

/* strtod/atof: liczba dziesiętna (znak, cyfry, ułamek, wykładnik) liczona jako float; z -D CC_DOUBLE64 wynik jest zapisany jako 8-bajtowy
   double, ale precyzja to float (zob. docs/minic.md). Bez cyfr zwraca 0 i ustawia *end na początek napisu. */
double strtod(const uchar *s, uchar **end) {
    const uchar *p = s;
    float f;
    while (isspace(*p)) p++;
    if (!scan_number(&p, &f, 1)) {
        if (end) *end = (uchar *)s;
        return 0;
    }
    if (end) *end = (uchar *)p;
    return f;
}

double atof(const uchar *s) {
    return strtod(s, 0);
}
