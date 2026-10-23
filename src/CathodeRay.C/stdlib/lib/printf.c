/* printf/sprintf: dodatkowe argumenty to 16-bit w cc_arg2..cc_arg6, więc definicja ma stałą liczbę parametrów,
   a nagłówek deklaruje wersję wariadyczną. Formatter jest we wspólnym nagłówku _format.h. */
#include <_format.h>

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
