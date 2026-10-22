/* stdio.h — konsola stub (bufor __io_buf), puts z nową linią, printf/sprintf do 5/4 argumentów.
   Formaty: %d %u %x %c %s %% oraz %ld %lu %lx (long zajmuje dwa z pięciu argumentów). */
#ifndef _STDIO_H
#define _STDIO_H
#include <stddef.h>
#define EOF (-1)
void putchar(uchar c);
int getchar();
int sscanf(const uchar *str, const uchar *fmt, ...);
void puthex(uchar b);
void putdec(int v);
void putstr(const uchar *s);
void puts(const uchar *s);
int printf(const uchar *fmt, ...);
int sprintf(uchar *buf, const uchar *fmt, ...);
#endif
