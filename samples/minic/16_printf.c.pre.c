typedef unsigned char uchar;
typedef unsigned int uint;
typedef unsigned long ulong;
// 16_printf.c — stdio: printf (%d %x %c %s), putchar, puts; konsola to bufor __io_buf.
// Konsola: "sum=15 hex=ff x name\ndone\n", wynik = 20 (liczba znaków pierwszego printf).
#include <stdio.h>

int main() {
    int n = printf("sum=%d hex=%x %c %s", 15, 255, 'x', "name");
    putchar(10);
    puts("done");
    return n;
}
