/* rt_shift.c — przesunięcia 16-bitowe o zmienną liczbę pozycji. */

uint __cc_shl(uint a, uchar n) {
    while (n) {
        a = a << 1;
        n = n - 1;
    }
    return a;
}

uint __cc_shr(uint a, uchar n) {
    while (n) {
        a = a >> 1;
        n = n - 1;
    }
    return a;
}

uint __cc_sar(uint a, uchar n) {
    uint s = a & 0x8000;
    while (n) {
        a = (a >> 1) | s;
        n = n - 1;
    }
    return a;
}
