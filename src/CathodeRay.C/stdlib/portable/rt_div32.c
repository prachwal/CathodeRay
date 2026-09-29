/* rt_div32.c — dzielenie i reszta 32-bitowe. */

ulong __cc_rem32;

ulong __cc_divu32(ulong a, ulong b) {
    ulong q = 0;
    ulong r = 0;
    uchar i = 32;
    if (b == 0) {
        __cc_rem32 = 0;
        return 0;
    }
    while (i) {
        r = (r << 1) | (a >> 31);
        a = a << 1;
        q = q << 1;
        if (r >= b) {
            r = r - b;
            q = q | 1;
        }
        i = i - 1;
    }
    __cc_rem32 = r;
    return q;
}

ulong __cc_modu32(ulong a, ulong b) {
    __cc_divu32(a, b);
    return __cc_rem32;
}

ulong __cc_divs32(ulong a, ulong b) {
    uchar neg = 0;
    if (a & 0x80000000) {
        a = 0 - a;
        neg = 1;
    }
    if (b & 0x80000000) {
        b = 0 - b;
        neg = neg ^ 1;
    }
    a = __cc_divu32(a, b);
    if (neg) {
        a = 0 - a;
    }
    return a;
}

ulong __cc_mods32(ulong a, ulong b) {
    uchar neg = 0;
    if (a & 0x80000000) {
        a = 0 - a;
        neg = 1;
    }
    if (b & 0x80000000) {
        b = 0 - b;
    }
    a = __cc_modu32(a, b);
    if (neg) {
        a = 0 - a;
    }
    return a;
}
