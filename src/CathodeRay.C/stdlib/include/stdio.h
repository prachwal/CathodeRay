/* stdio.h — konsola stub (bufor __io_buf), puts z nową linią, printf/sprintf do 5/4 argumentów.
   Formaty: %d %u %x %c %s %%. */
#ifndef _STDIO_H
#define _STDIO_H
#include <stddef.h>
void putchar(uchar c);
void puthex(uchar b);
void putdec(int v);
void putstr(const uchar *s);
void puts(const uchar *s);
int printf(const uchar *fmt, ...);
int sprintf(uchar *buf, const uchar *fmt, ...);
#endif
