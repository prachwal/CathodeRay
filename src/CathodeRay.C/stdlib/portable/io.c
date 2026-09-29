/* io.c — konsola dla celów kompilowanych z C (bufor __io_buf + kursor __io_cur, jak samples/stub/lib/io.s):
   putchar dopisuje znak, puthex bajt jako 2 znaki HEX, putdec liczbę int ze znakiem. */

uchar __io_buf[256];
uchar __io_cur;

void putchar(uchar c) {
    __io_buf[__io_cur] = c;
    __io_cur++;
}

void __io_nibble(uchar n) {
    n = (n & 15) + 48;
    if (n >= 58) {
        n = n + 7;
    }
    putchar(n);
}

void puthex(uchar b) {
    __io_nibble(b >> 4);
    __io_nibble(b);
}

void putdec(int v) {
    uchar digits[5];
    uchar n = 0;
    uint u = v;
    if (v < 0) {
        putchar(45);
        u = 0 - u;
    }
    if (u == 0) {
        putchar(48);
        return;
    }
    while (u) {
        digits[n] = (u % 10) + 48;
        n++;
        u = u / 10;
    }
    while (n) {
        n--;
        putchar(digits[n]);
    }
}
