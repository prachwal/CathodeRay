#include <string.h>

int strcmp(const uchar *a, const uchar *b) {
    while (*a && *a == *b) {
        a++;
        b++;
    }
    int x = *a;
    return x - *b;
}
