/* string.h — funkcje na napisach i pamięci (strchr bierze uchar *; pamięć przez void *). */
#ifndef _STRING_H
#define _STRING_H
#include <stddef.h>
uint strlen(const uchar *s);
uchar *strcpy(uchar *dst, const uchar *src);
uchar *strncpy(uchar *dst, const uchar *src, uint n);
uchar *strcat(uchar *dst, const uchar *src);
int strcmp(const uchar *a, const uchar *b);
int strncmp(const uchar *a, const uchar *b, uint n);
uchar *strchr(uchar *s, uchar c);
void *memcpy(void *dst, const void *src, size_t n);
void *memset(void *dst, uchar value, size_t n);
int memcmp(const void *a, const void *b, size_t n);
#endif
