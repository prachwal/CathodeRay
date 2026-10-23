#include <string.h>

/* strchr: jak w C przyjmuje const napis, a zwraca zwykły wskaźnik do znaku w nim. */
uchar *strchr(const uchar *s, uchar c) {
    while (*s) {
        if (*s == c) return (uchar *)s;
        s++;
    }
    if (c == 0) return (uchar *)s;
    return 0;
}
