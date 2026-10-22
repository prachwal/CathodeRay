#include <stdio.h>

/* getchar: wejście to bufor __in_buf zakończony zerem (host wpisuje tekst przed startem; bufor ma inicjalizator, więc leży w DATA i crt0 go nie zeruje); po zerze zwraca EOF.
   Kursor __in_cur ma 8 bitów, więc wejście ma najwyżej 255 znaków. */
uchar __in_buf[256] = {0};
uchar __in_cur;

int getchar() {
    uchar c = __in_buf[__in_cur];
    if (c == 0) return EOF;
    __in_cur++;
    return c;
}
