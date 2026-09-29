/* rt_div.c — dzielenie i reszta 16-bitowe bez znaku i ze znakiem. */

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

uint __cc_divs(uint a, uint b) {
    uchar neg = 0;
    if (a & 0x8000) {
        a = 0 - a;
        neg = 1;
    }
    if (b & 0x8000) {
        b = 0 - b;
        neg = neg ^ 1;
    }
    a = __cc_divu(a, b);
    if (neg) {
        a = 0 - a;
    }
    return a;
}

uint __cc_mods(uint a, uint b) {
    uchar neg = 0;
    if (a & 0x8000) {
        a = 0 - a;
        neg = 1;
    }
    if (b & 0x8000) {
        b = 0 - b;
    }
    a = __cc_modu(a, b);
    if (neg) {
        a = 0 - a;
    }
    return a;
}
