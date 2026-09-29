#include <string.h>

void *memcpy(void *dst, const void *src, size_t n) {
    uchar *d = dst;
    const uchar *s = src;
    while (n) {
        *d++ = *s++;
        n--;
    }
    return dst;
}
