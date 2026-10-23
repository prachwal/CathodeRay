/* snprintf: ten sam formatter co printf (_format.h) z ograniczeniem liczby zapisanych znaków. Osobny moduł, żeby programy
   z samym printf nie płaciły za ten kod. */
#define FMT_LIMIT 1
#include <_format.h>

/* Zapisuje najwyżej n-1 znaków i kończy zerem (n == 0: nic); wynik to długość pełnego tekstu, jak w C99. */
int snprintf(uchar *buf, uint n, const uchar *fmt, int a1, int a2, int a3) {
    uchar *end;
    dst = buf;
    limit = n > 0 ? n - 1 : 0;
    written = 0;
    format(fmt, a1, a2, a3, 0, 0);
    end = dst;
    dst = 0;
    if (n > 0) *end = 0;
    return written;
}
