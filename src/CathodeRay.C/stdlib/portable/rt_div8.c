/* rt_div8.c — dzielenie i reszta 8-bitowe bez znaku (reszta w uint, bo podwojona może przekroczyć 255). */

uchar __cc_rem8;

uchar __cc_divu8(uchar a, uchar b) {
    uchar q = 0;
    uint r = 0;
    uchar i = 8;
    if (b == 0) {
        __cc_rem8 = 0;
        return 0;
    }
    while (i) {
        r = (r << 1) | (a >> 7);
        a = a << 1;
        q = q << 1;
        if (r >= b) {
            r = r - b;
            q = q | 1;
        }
        i = i - 1;
    }
    __cc_rem8 = r;
    return q;
}

uchar __cc_modu8(uchar a, uchar b) {
    __cc_divu8(a, b);
    return __cc_rem8;
}
