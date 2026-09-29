#include <stdlib.h>

uchar *itoa(int value, uchar *buf, uchar base) {
    uchar tmp[17];
    uchar n = 0;
    uchar i = 0;
    uchar negative = 0;
    uint u = value;
    if (base == 10 && value < 0) {
        negative = 1;
        u = 0 - value;
    }
    if (u == 0) {
        tmp[n] = '0';
        n++;
    }
    while (u > 0) {
        uchar d = u % base;
        tmp[n] = d < 10 ? '0' + d : 'a' + d - 10;
        n++;
        u = u / base;
    }
    if (negative) {
        buf[i] = '-';
        i++;
    }
    while (n > 0) {
        n--;
        buf[i] = tmp[n];
        i++;
    }
    buf[i] = 0;
    return buf;
}
