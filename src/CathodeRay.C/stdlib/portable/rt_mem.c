/* rt_mem.c — kopiowanie i wypełnianie bloków pamięci. */

void __cc_copy(uchar *d, uchar *s, uint n) {
    while (n) {
        *d = *s;
        d = d + 1;
        s = s + 1;
        n = n - 1;
    }
}

void __cc_fill(uchar *d, uchar v, uint n) {
    while (n) {
        *d = v;
        d = d + 1;
        n = n - 1;
    }
}
