// C4: strtod i sscanf %lg z -D CC_DOUBLE64: wynik jest 8-bajtowym double (obliczenia w precyzji float),
// a wzorzec NaN-boxingu (unia double/uint64) zachowuje bity.
// expect-args: -D CC_DOUBLE64
// expect-result: 324
#include <stdio.h>
#include <stdlib.h>

union box {
    double x;
    unsigned long long bits;
};

int main(void)
{
    double a = strtod("2.5", 0);
    double b;
    int n = 0;
    union box u;
    int tagged;

    sscanf("7.25 x", "%lg%n", &b, &n);
    u.bits = 0x7ffc000000000005ULL;                    /* "pointer" w NaN-boxingu: bity muszą przeżyć kopiowanie double */
    {
        double copy = u.x;
        u.x = copy;
    }
    tagged = u.bits == 0x7ffc000000000005ULL;
    return (int)(a * 100) + (int)b * 10 + n + (tagged ? 0 : 1000);
}
