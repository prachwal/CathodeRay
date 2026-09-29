#include <string.h>

uchar *strncpy(uchar *dst, const uchar *src, uint n) {
    uchar *d = dst;
    while (n && *src) {
        *d++ = *src++;
        n--;
    }
    while (n) {
        *d++ = 0;
        n--;
    }
    return dst;
}
