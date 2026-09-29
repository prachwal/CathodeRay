#include <string.h>

uint strlen(const uchar *s) {
    uint n = 0;
    while (s[n]) n++;
    return n;
}
