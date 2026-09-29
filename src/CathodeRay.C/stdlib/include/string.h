/* string.h — funkcje na napisach i pamięci (uchar zamiast char/void*; strchr bierze uchar *). */
#ifndef _STRING_H
#define _STRING_H
#define NULL 0
uint strlen(const uchar *s);
uchar *strcpy(uchar *dst, const uchar *src);
uchar *strncpy(uchar *dst, const uchar *src, uint n);
uchar *strcat(uchar *dst, const uchar *src);
int strcmp(const uchar *a, const uchar *b);
int strncmp(const uchar *a, const uchar *b, uint n);
uchar *strchr(uchar *s, uchar c);
uchar *memcpy(uchar *dst, const uchar *src, uint n);
uchar *memset(uchar *dst, uchar value, uint n);
int memcmp(const uchar *a, const uchar *b, uint n);
#endif
