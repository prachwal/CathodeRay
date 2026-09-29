// long/ulong: stałe z przyrostkami, arytmetyka 32-bitowa, porównania, tablica, printf %ld/%lu/%lx.
#include <stdio.h>

long fib(int n) {
    long a = 0;
    long b = 1;
    int i = 0;
    while (i < n) {
        long t = a + b;
        a = b;
        b = t;
        i++;
    }
    return a;
}

ulong factorial(uchar n) {
    ulong r = 1;
    uchar i = 2;
    while (i <= n) {
        r = r * i;
        i++;
    }
    return r;
}

long samples[4] = {100000L, 0 - 3, 2000000000L, 7};

int main() {
    long f = fib(40);                       // 102334155
    ulong fact = factorial(12);             // 479001600
    long mix = f / 1000 - fact % 1000L;     // 102334 - 600
    long sum = 0;
    int k;
    for (k = 0; k < 4; k++) {
        sum = sum + samples[k];
    }
    printf("fib=%ld fact=%lx\n", f, fact);
    printf("sum=%ld big=%lu\n", sum, 4000000000);
    return (int)(mix >> 4) + (sum > 2000000000L) + (fact > 0x10000000UL);
}
