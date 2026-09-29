/* rt_mul8.c — mnożenie 8-bitowe (wynik uchar), tańsze od 16-bitowego. */

uchar __cc_mul8(uchar a, uchar b) {
    uchar r = 0;
    while (b) {
        if (b & 1) {
            r = r + a;
        }
        a = a << 1;
        b = b >> 1;
    }
    return r;
}
