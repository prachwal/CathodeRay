/* rt_shift32.c — przesunięcia 32-bitowe o zmienną liczbę pozycji. */

ulong __cc_shl32(ulong a, uchar n) {
    while (n) {
        a = a << 1;
        n = n - 1;
    }
    return a;
}

ulong __cc_shr32(ulong a, uchar n) {
    while (n) {
        a = a >> 1;
        n = n - 1;
    }
    return a;
}

ulong __cc_sar32(ulong a, uchar n) {
    ulong s = a & 0x80000000;
    while (n) {
        a = (a >> 1) | s;
        n = n - 1;
    }
    return a;
}
