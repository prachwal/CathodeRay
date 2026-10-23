#include <string.h>

/* memmove: kopiowanie z nakładaniem się obszarów (do przodu albo od końca). */
void *memmove(void *dst, const void *src, size_t n) {
    uchar *d = dst;
    const uchar *s = src;
    if (d < s) {
        while (n) {
            *d++ = *s++;
            n--;
        }
    } else {
        d += n;
        s += n;
        while (n) {
            d--;
            s--;
            *d = *s;
            n--;
        }
    }
    return dst;
}
