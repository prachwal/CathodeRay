/* rt_ll64.c — long long (64 bity) na połówkach 32-bitowych. Argumenty to adresy obiektów 8-bajtowych w kolejności bajtów celu
   (LE: młodsze słowo pierwsze, BE: starsze), wynik zapisywany pod adres pierwszego argumentu (może się pokrywać z operandem). */

static uchar __ll_be() {
    ulong t = 1;
    return *(uchar *)&t == 0;
}

static ulong __ll_lo(ulong *p) {
    return p[__ll_be()];
}

static ulong __ll_hi(ulong *p) {
    return p[1 - __ll_be()];
}

static void __ll_put(ulong *p, ulong l, ulong h) {
    p[__ll_be()] = l;
    p[1 - __ll_be()] = h;
}

static void __ll_neg(ulong *p) {
    ulong l = __ll_lo(p);
    ulong h = 0 - __ll_hi(p);
    if (l != 0) {
        h = h - 1;
    }
    __ll_put(p, 0 - l, h);
}

/* |src| do dst; wynik 1, gdy src był ujemny */
static uchar __ll_abs(ulong *dst, ulong *src) {
    dst[0] = src[0];
    dst[1] = src[1];
    if (__ll_hi(src) & 0x80000000) {
        __ll_neg(dst);
        return 1;
    }
    return 0;
}

void __cc_mul64(ulong *r, ulong *a, ulong *b) {
    ulong al = __ll_lo(a);
    ulong ah = __ll_hi(a);
    ulong bl = __ll_lo(b);
    ulong bh = __ll_hi(b);
    ulong rl = 0;
    ulong rh = 0;
    ulong t;
    uchar i;
    for (i = 0; i < 64; i++) {
        if (bl == 0 && bh == 0) {
            break;
        }
        if (bl & 1) {
            t = rl + al;
            if (t < rl) {
                rh = rh + 1;
            }
            rl = t;
            rh = rh + ah;
        }
        bl = (bl >> 1) | (bh << 31);
        bh = bh >> 1;
        ah = (ah << 1) | (al >> 31);
        al = al << 1;
    }
    __ll_put(r, rl, rh);
}

/* q = n / d, m = n % d (bez znaku) */
static void __ll_udm(ulong *n, ulong *d, ulong *q, ulong *m) {
    ulong nl = __ll_lo(n);
    ulong nh = __ll_hi(n);
    ulong dl = __ll_lo(d);
    ulong dh = __ll_hi(d);
    ulong ql = 0;
    ulong qh = 0;
    ulong rl = 0;
    ulong rh = 0;
    ulong top;
    uchar i;
    for (i = 0; i < 64; i++) {
        top = rh >> 31;
        rh = (rh << 1) | (rl >> 31);
        rl = (rl << 1) | (nh >> 31);
        nh = (nh << 1) | (nl >> 31);
        nl = nl << 1;
        qh = (qh << 1) | (ql >> 31);
        ql = ql << 1;
        if (top || rh > dh || (rh == dh && rl >= dl)) {
            if (rl < dl) {
                rh = rh - 1;
            }
            rl = rl - dl;
            rh = rh - dh;
            ql = ql | 1;
        }
    }
    __ll_put(q, ql, qh);
    __ll_put(m, rl, rh);
}

void __cc_divu64(ulong *r, ulong *a, ulong *b) {
    ulong m[2];
    __ll_udm(a, b, r, m);
}

void __cc_modu64(ulong *r, ulong *a, ulong *b) {
    ulong q[2];
    __ll_udm(a, b, q, r);
}

void __cc_divs64(ulong *r, ulong *a, ulong *b) {
    ulong x[2];
    ulong y[2];
    ulong q[2];
    ulong m[2];
    uchar sa = __ll_abs(x, a);
    uchar sb = __ll_abs(y, b);
    __ll_udm(x, y, q, m);
    if (sa != sb) {
        __ll_neg(q);
    }
    r[0] = q[0];
    r[1] = q[1];
}

void __cc_mods64(ulong *r, ulong *a, ulong *b) {
    ulong x[2];
    ulong y[2];
    ulong q[2];
    ulong m[2];
    uchar sa = __ll_abs(x, a);
    __ll_abs(y, b);
    __ll_udm(x, y, q, m);
    if (sa) {
        __ll_neg(m);
    }
    r[0] = m[0];
    r[1] = m[1];
}

void __cc_shl64(ulong *r, ulong *a, uchar n) {
    ulong al = __ll_lo(a);
    ulong ah = __ll_hi(a);
    n = n & 63;
    if (n >= 32) {
        ah = al << (uchar)(n - 32);
        al = 0;
    } else if (n) {
        ah = (ah << n) | (al >> (uchar)(32 - n));
        al = al << n;
    }
    __ll_put(r, al, ah);
}

void __cc_shr64(ulong *r, ulong *a, uchar n) {
    ulong al = __ll_lo(a);
    ulong ah = __ll_hi(a);
    n = n & 63;
    if (n >= 32) {
        al = ah >> (uchar)(n - 32);
        ah = 0;
    } else if (n) {
        al = (al >> n) | (ah << (uchar)(32 - n));
        ah = ah >> n;
    }
    __ll_put(r, al, ah);
}

void __cc_sar64(ulong *r, ulong *a, uchar n) {
    ulong al = __ll_lo(a);
    ulong ah = __ll_hi(a);
    long sh = (long)ah;
    n = n & 63;
    if (n >= 32) {
        al = (ulong)(sh >> (uchar)(n - 32));
        ah = (ulong)(sh >> 31);
    } else if (n) {
        al = (al >> n) | (ah << (uchar)(32 - n));
        ah = (ulong)(sh >> n);
    }
    __ll_put(r, al, ah);
}
