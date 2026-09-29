// short/unsigned/signed char, volatile, inline, register i pola bitowe. Wynik: 4677.
#include <stdio.h>

typedef unsigned short word;

struct Flags {
    uchar ready : 1;
    uchar mode : 3;
    uchar level : 4;
    int delta : 6;
};

volatile uchar port;

static inline short twice(short x) { return x + x; }

int main() {
    register unsigned char small = 200;
    signed char down = -100;
    unsigned short big = 60000;
    word w = 40000;
    struct Flags f;
    int total = 0;
    f.ready = 1;
    f.mode = 5;
    f.level = 12;
    f.delta = -7;
    f.level += 3;
    f.mode++;
    port = f.mode;
    port = port + 1;
    if (down < 0 && (down >> 2) == -25) total += 1;
    if ((int)small + 100 == 300) total += 2;
    if (big > w) total += 4;
    total += twice(50) + port * 10 + (int)f.level * 100 + f.delta + 7;
    puts(f.ready ? "flags ok" : "flags bad");
    return total + sizeof(struct Flags) * 1000;
}
