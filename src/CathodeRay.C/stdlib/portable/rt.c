/* rt.c — moduł wykonawczy celów bez mnożenia/dzielenia/przesunięć o zmienną: wołany przez Legalizer.
   Pisany tak, by sam używał tylko dodawania, odejmowania, operacji bitowych i przesunięć o stałą. */

uint __cc_rem;

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
