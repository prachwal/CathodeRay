/* rt_div.c — dzielenie i reszta 16-bitowe bez znaku. */

uint __cc_rem;

uint __cc_divu(uint a, uint b) {
    uint q = 0;
    uint r = 0;
    uchar i = 16;
    if (b == 0) {
        __cc_rem = 0;
        return 0;
    }
    while (i) {
        r = (r << 1) | (a >> 15);
        a = a << 1;
        q = q << 1;
        if (r >= b) {
            r = r - b;
            q = q | 1;
        }
        i = i - 1;
    }
    __cc_rem = r;
    return q;
}

uint __cc_modu(uint a, uint b) {
    __cc_divu(a, b);
    return __cc_rem;
}
