#include <string.h>

uchar *memset(uchar *dst, uchar value, uint n) {
    uchar *d = dst;
    while (n) {
        *d++ = value;
        n--;
    }
    return dst;
}
