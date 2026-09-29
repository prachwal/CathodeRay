#include <string.h>

uchar *strcat(uchar *dst, const uchar *src) {
    uchar *d = dst;
    while (*d) d++;
    while (*src) {
        *d++ = *src++;
    }
    *d = 0;
    return dst;
}
