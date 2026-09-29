// float liczony programowo: pole koła, średnia, konwersje i porównania. Wynik: 1078.
#include <stdio.h>
#include <stdlib.h>

float area(float r) { return 3.14159f * r * r; }

int main() {
    uchar buf[24];
    float a = area(2.5);
    float mean = (a + 10 + 20.5) / 3;
    int whole = (int)a;
    int total = 0;
    puts(ftoa(a, buf));
    puts(ftoa(mean, buf));
    puts(ftoa(-0.125, buf));
    if (a > 19.6 && a < 19.7) total += 100;
    if (mean != a) total += 20;
    if ((float)whole < a) total += 8;
    return total + whole * 50;
}
