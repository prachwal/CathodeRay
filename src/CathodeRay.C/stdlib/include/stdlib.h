/* stdlib.h — abs/min/max, konwersje liczb i generator pseudolosowy (xorshift 16-bit). */
#ifndef _STDLIB_H
#define _STDLIB_H
#define NULL 0
#define RAND_MAX 65535
int abs(int x);
int min(int a, int b);
int max(int a, int b);
int atoi(const uchar *s);
uchar *itoa(int value, uchar *buf, uchar base);
void srand(uint seed);
uint rand();
#endif
