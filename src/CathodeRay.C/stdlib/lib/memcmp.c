#include <string.h>

int memcmp(const uchar *a, const uchar *b, uint n) {
    while (n) {
        if (*a != *b) {
            int x = *a;
            return x - *b;
        }
        a++;
        b++;
        n--;
    }
    return 0;
}
