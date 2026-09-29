#include <string.h>

uchar *memcpy(uchar *dst, const uchar *src, uint n) {
    uchar *d = dst;
    while (n) {
        *d++ = *src++;
        n--;
    }
    return dst;
}
