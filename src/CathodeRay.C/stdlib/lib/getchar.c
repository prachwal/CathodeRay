#include <stdio.h>

/* getchar: wejście to bufor __in_buf zakończony zerem (host wpisuje tekst przed startem); po zerze albo po 256 bajtach zwraca EOF.
   Bufor ma inicjalizator, więc leży w DATA i crt0 go nie zeruje; kursor jest 16-bitowy, więc pełny bufor bez zera nie zawija się. */
uchar __in_buf[256] = {0};
uint __in_cur;

int getchar() {
    uchar c;
    if (__in_cur >= 256) return EOF;
    c = __in_buf[__in_cur];
    if (c == 0) return EOF;
    __in_cur++;
    return c;
}
