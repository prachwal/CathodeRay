#include <string.h>

int memcmp(const void *a, const void *b, size_t n) {
    const uchar *x = a;
    const uchar *y = b;
    while (n) {
        if (*x != *y) {
            int v = *x;
            return v - *y;
        }
        x++;
        y++;
        n--;
    }
    return 0;
}
