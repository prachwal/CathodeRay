#include <stdlib.h>

uchar *ulltoa(unsigned long long value, uchar *buf, uchar base) {
    uchar tmp[65];
    uchar n = 0;
    uchar i = 0;
    uchar d;
    if (value == 0) {
        tmp[n] = '0';
        n++;
    }
    while (value != 0) {
        d = (uchar)(value % base);
        tmp[n] = d < 10 ? '0' + d : 'a' + d - 10;
        n++;
        value = value / base;
    }
    while (n > 0) {
        n--;
        buf[i] = tmp[n];
        i++;
    }
    buf[i] = 0;
    return buf;
}

uchar *lltoa(long long value, uchar *buf, uchar base) {
    if (base == 10 && value < 0) {
        buf[0] = '-';
        ulltoa((unsigned long long)(0 - value), buf + 1, base);
        return buf;
    }
    return ulltoa((unsigned long long)value, buf, base);
}
