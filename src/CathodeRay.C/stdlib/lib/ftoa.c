#include <stdlib.h>

/* ftoa: float jako tekst dziesiętny (część całkowita i 6 cyfr ułamka, ułamek zaokrąglony, "inf" od 2^32).
   Bity IEEE-754 rozbierane na całkowite, więc nie wciąga procedur zmiennoprzecinkowych. */
uchar *ftoa(float f, uchar *buf) {
    ulong bits = *(ulong *)&f;
    int e = (int)((bits >> 23) & 0xFF) - 127;
    ulong m = (bits & 0x7FFFFF) | 0x800000;
    ulong ip = 0;
    ulong frac = 0;
    ulong one;
    uchar tmp[10];
    uchar n = 0;
    uchar fb = 0;
    uchar i = 0;
    uchar k;
    if (((bits >> 23) & 0xFF) == 0) {
        m = 0;
        e = 0;
    }
    if (bits & 0x80000000) {
        buf[i] = '-';
        i++;
    }
    if (e >= 32) {
        buf[i] = 'i';
        buf[i + 1] = 'n';
        buf[i + 2] = 'f';
        buf[i + 3] = 0;
        return buf;
    }
    if (e >= 23) {
        ip = m << (uchar)(e - 23);
    } else if (e >= 0) {
        fb = 23 - e;
        ip = m >> fb;
        frac = m & (((ulong)1 << fb) - 1);
    } else {
        fb = 24;
        if (e > -25) frac = m >> (uchar)(0 - e - 1);
    }
    if (fb) {
        one = (ulong)1 << fb;
        frac = frac + (one >> 1) / 1000000;
        if (frac >= one) {
            frac = frac - one;
            ip++;
        }
    }
    if (ip == 0) {
        tmp[n] = '0';
        n++;
    }
    while (ip > 0) {
        tmp[n] = '0' + (uchar)(ip % 10);
        n++;
        ip = ip / 10;
    }
    while (n > 0) {
        n--;
        buf[i] = tmp[n];
        i++;
    }
    buf[i] = '.';
    i++;
    for (k = 0; k < 6; k++) {
        if (fb) {
            frac = frac * 10;
            buf[i] = '0' + (uchar)(frac >> fb);
            frac = frac & (((ulong)1 << fb) - 1);
        } else {
            buf[i] = '0';
        }
        i++;
    }
    buf[i] = 0;
    return buf;
}
