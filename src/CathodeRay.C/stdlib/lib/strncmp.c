#include <string.h>

int strncmp(const uchar *a, const uchar *b, uint n) {
    while (n && *a && *a == *b) {
        a++;
        b++;
        n--;
    }
    if (n == 0) return 0;
    int x = *a;
    return x - *b;
}
