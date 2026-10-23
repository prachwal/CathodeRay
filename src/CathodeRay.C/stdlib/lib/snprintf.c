/* snprintf: ten sam formatter co printf (_format.h) z ograniczeniem liczby zapisanych znaków oraz %f/%g/%e dla float (2 słowa
   argumentów; flagi, szerokość i precyzja są pomijane, %g/%e obcina końcowe zera ułamka; printf nie ma float, żeby nie rosnąć). Osobny moduł, żeby programy
   z samym printf nie płaciły za ten kod. */
#define FMT_LIMIT 1
#define FMT_FLOAT 1
#include <_format.h>

/* Zapisuje najwyżej n-1 znaków i kończy zerem (n == 0: nic); wynik to długość pełnego tekstu, jak w C99. */
int snprintf(uchar *buf, uint n, const uchar *fmt, int a1, int a2, int a3) {
    uchar *end;
    uchar sink;
    /* snprintf(NULL, 0, ...) tylko liczy znaki: dst nie może być zerem, bo to oznaczałoby konsolę */
    dst = buf ? buf : &sink;
    limit = buf && n > 0 ? n - 1 : 0;
    written = 0;
    format(fmt, a1, a2, a3, 0, 0);
    end = dst;
    dst = 0;
    if (buf && n > 0) *end = 0;
    return written;
}
