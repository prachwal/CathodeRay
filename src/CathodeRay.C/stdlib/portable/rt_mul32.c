/* rt_mul32.c — mnożenie 32-bitowe. */

ulong __cc_mul32(ulong a, ulong b) {
    ulong r = 0;
    while (b) {
        if (b & 1) {
            r = r + a;
        }
        a = a << 1;
        b = b >> 1;
    }
    return r;
}
