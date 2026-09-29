#include <string.h>

uchar *strchr(uchar *s, uchar c) {
    while (*s) {
        if (*s == c) return s;
        s++;
    }
    if (c == 0) return s;
    return 0;
}
