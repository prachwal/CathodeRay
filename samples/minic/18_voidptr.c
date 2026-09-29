// void *, size_t, NULL, offsetof i pamięciowe funkcje biblioteki na void *. Wynik: 1103.
#include <stddef.h>
#include <string.h>

struct Pair { uchar tag; int value; uchar pad[3]; };

int sum(void *items, size_t count) {
    int *p = items;
    int total = 0;
    size_t i = 0;
    while (i < count) {
        total = total + p[i];
        i++;
    }
    return total;
}

int main() {
    int a[4] = {100, 200, 300, 400};
    int b[4];
    void *dst = b;
    memcpy(dst, a, sizeof(a));
    memset(&a[0], 0, 2);                       // zeruje młodszy bajt a[0]
    void *nothing = NULL;
    int r = sum(b, 4);                          // 1000
    r = r + (nothing == NULL) + offsetof(struct Pair, value) + (int)sizeof(void *) + memcmp(a, b, 2) * 0;
    return r + (int)offsetof(struct Pair, pad) + 100 - 4;
}
