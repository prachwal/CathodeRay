/* stdlib.h — abs/min/max, konwersje liczb (itoa, ftoa: float z 6 cyframi ułamka; printf nie ma %f, żeby nie powiększać każdego programu) i generator pseudolosowy (xorshift 16-bit). */
#ifndef _STDLIB_H
#define _STDLIB_H
#include <stddef.h>
#define RAND_MAX 65535
int abs(int x);
int min(int a, int b);
int max(int a, int b);
int atoi(const uchar *s);
uchar *itoa(int value, uchar *buf, uchar base);
uchar *ftoa(float value, uchar *buf);
uchar *lltoa(long long value, uchar *buf, uchar base);
uchar *ulltoa(unsigned long long value, uchar *buf, uchar base);
void srand(uint seed);
uint rand();
#endif
