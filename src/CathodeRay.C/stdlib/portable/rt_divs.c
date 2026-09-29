/* rt_divs.c — dzielenie i reszta 16-bitowe ze znakiem (na wersjach bez znaku z rt_div). */

uint __cc_divu(uint a, uint b);
uint __cc_modu(uint a, uint b);

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
