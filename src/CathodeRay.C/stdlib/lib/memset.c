#include <string.h>

void *memset(void *dst, uchar value, size_t n) {
    uchar *d = dst;
    while (n) {
        *d++ = value;
        n--;
    }
    return dst;
}
