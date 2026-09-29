#include <string.h>

uchar *strcpy(uchar *dst, const uchar *src) {
    uchar *d = dst;
    while (*src) {
        *d++ = *src++;
    }
    *d = 0;
    return dst;
}
