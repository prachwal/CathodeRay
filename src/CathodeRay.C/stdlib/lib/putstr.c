#include <stdio.h>

void putstr(const uchar *s) {
    while (*s) {
        putchar(*s);
        s++;
    }
}
