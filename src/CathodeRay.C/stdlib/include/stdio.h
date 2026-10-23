/* stdio.h — konsola stub (bufor __io_buf), puts z nową linią, printf/sprintf do 5/4 argumentów.
   Formaty: %d %u %x %c %s %% oraz %ld %lu %lx (long zajmuje dwa z pięciu argumentów). */
#ifndef _STDIO_H
#define _STDIO_H
#include <stddef.h>
#define EOF (-1)

/* Strumienie: stdin to bufor wejściowy hosta (getchar), stdout i stderr trafiają na konsolę (__io_buf); fopen nie ma
   systemu plików i zwraca NULL. printf-podobne funkcje przyjmują do 5 słów argumentów (fprintf 4, snprintf 3). */
typedef struct {
    uchar kind;
    int unget;
} FILE;
extern FILE __stdin_f;
extern FILE __stdout_f;
extern FILE __stderr_f;
#define stdin (&__stdin_f)
#define stdout (&__stdout_f)
#define stderr (&__stderr_f)
FILE *fopen(const uchar *path, const uchar *mode);
int fclose(FILE *f);
int fflush(FILE *f);
int fputc(int c, FILE *f);
int putc(int c, FILE *f);
int fputs(const uchar *s, FILE *f);
int fgetc(FILE *f);
int getc(FILE *f);
int ungetc(int c, FILE *f);
int fprintf(FILE *f, const uchar *fmt, ...);
int snprintf(uchar *buf, uint n, const uchar *fmt, ...);
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
