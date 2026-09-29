// long long: liczby Fibonacciego do 90, silnia 20, dzielenie i przesuniecia. Wynik: 2020.
#include <stdio.h>
#include <stdlib.h>

long long fib(int n) {
    long long a = 0;
    long long b = 1;
    long long t;
    while (n > 0) {
        t = a + b;
        a = b;
        b = t;
        n--;
    }
    return a;
}

unsigned long long fact(int n) {
    unsigned long long r = 1;
    while (n > 1) {
        r = r * n;
        n--;
    }
    return r;
}

int main() {
    uchar buf[24];
    long long f = fib(90);
    unsigned long long p = fact(20);
    int total = 0;
    puts(lltoa(f, buf, 10));
    puts(ulltoa(p, buf, 10));
    puts(lltoa(f / 1000000007 - (f >> 20), buf, 10));
    if (f % 1000 == 120) total += 20;
    if ((p >> 40) == 2212711) total += 2000;
    return total;
}
