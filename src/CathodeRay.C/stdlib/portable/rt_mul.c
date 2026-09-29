/* rt_mul.c — mnożenie 16-bitowe (shift-add). */

uint __cc_mul(uint a, uint b) {
    uint r = 0;
    while (b) {
        if (b & 1) {
            r = r + a;
        }
        a = a << 1;
        b = b >> 1;
    }
    return r;
}
