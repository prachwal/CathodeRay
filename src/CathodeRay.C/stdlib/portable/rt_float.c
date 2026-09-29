/* rt_float.c — float (IEEE-754 pojedynczej precyzji) liczony programowo. Argumenty i wyniki to bity w ulong.
   Uproszczenia: wynik obcinany (bez zaokrąglania), liczby zdenormalizowane traktowane jak zero, bez NaN/Inf w działaniach
   (nadmiar daje nieskończoność, niedomiar zero). */

ulong __cc_fpack(uchar sign, int e, ulong m) {
    ulong r = 0;
    if (sign) {
        r = 0x80000000;
    }
    if (e <= 0) {
        return r;
    }
    if (e >= 255) {
        return r | 0x7F800000;
    }
    return r | ((ulong)e << 23) | (m & 0x7FFFFF);
}

ulong __cc_fadd(ulong a, ulong b) {
    ulong t;
    ulong ma;
    ulong mb;
    uchar sa;
    uchar sb;
    int ea = (int)((a >> 23) & 0xFF);
    int eb = (int)((b >> 23) & 0xFF);
    int d;
    if (ea == 255) {
        return a;
    }
    if (eb == 255) {
        return b;
    }
    if (ea == 0) {
        return b;
    }
    if (eb == 0) {
        return a;
    }
    if ((a & 0x7FFFFFFF) < (b & 0x7FFFFFFF)) {
        t = a;
        a = b;
        b = t;
        d = ea;
        ea = eb;
        eb = d;
    }
    sa = (uchar)(a >> 31);
    sb = (uchar)(b >> 31);
    ma = (a & 0x7FFFFF) | 0x800000;
    mb = (b & 0x7FFFFF) | 0x800000;
    d = ea - eb;
    if (d > 24) {
        return a;
    }
    mb = mb >> (uchar)d;
    if (sa == sb) {
        ma = ma + mb;
        if (ma & 0x1000000) {
            ma = ma >> 1;
            ea = ea + 1;
        }
    } else {
        ma = ma - mb;
        if (ma == 0) {
            return 0;
        }
        while (!(ma & 0x800000)) {
            ma = ma << 1;
            ea = ea - 1;
        }
    }
    return __cc_fpack(sa, ea, ma);
}

ulong __cc_fsub(ulong a, ulong b) {
    return __cc_fadd(a, b ^ 0x80000000);
}

ulong __cc_fmul(ulong a, ulong b) {
    uchar s = (uchar)((a ^ b) >> 31);
    int ea = (int)((a >> 23) & 0xFF);
    int eb = (int)((b >> 23) & 0xFF);
    ulong ma;
    ulong mb;
    ulong hi = 0;
    ulong lo = 0;
    uchar i;
    int e;
    if (ea == 0 || eb == 0) {
        return __cc_fpack(s, 0, 0);
    }
    if (ea == 255 || eb == 255) {
        return __cc_fpack(s, 255, 0);
    }
    ma = (a & 0x7FFFFF) | 0x800000;
    mb = (b & 0x7FFFFF) | 0x800000;
    for (i = 0; i < 24; i++) {
        if (mb & 1) {
            hi = hi + ma;
        }
        lo = (lo >> 1) | ((hi & 1) << 23);
        hi = hi >> 1;
        mb = mb >> 1;
    }
    e = ea + eb - 127;
    if (hi & 0x800000) {
        return __cc_fpack(s, e + 1, hi);
    }
    return __cc_fpack(s, e, (hi << 1) | (lo >> 23));
}

ulong __cc_fdiv(ulong a, ulong b) {
    uchar s = (uchar)((a ^ b) >> 31);
    int ea = (int)((a >> 23) & 0xFF);
    int eb = (int)((b >> 23) & 0xFF);
    ulong ma;
    ulong mb;
    ulong q = 0;
    uchar i;
    int e;
    if (ea == 0 || eb == 255) {
        return __cc_fpack(s, 0, 0);
    }
    if (eb == 0 || ea == 255) {
        return __cc_fpack(s, 255, 0);
    }
    ma = (a & 0x7FFFFF) | 0x800000;
    mb = (b & 0x7FFFFF) | 0x800000;
    e = ea - eb + 127;
    if (ma < mb) {
        ma = ma << 1;
        e = e - 1;
    }
    for (i = 0; i < 24; i++) {
        q = q << 1;
        if (ma >= mb) {
            ma = ma - mb;
            q = q | 1;
        }
        ma = ma << 1;
    }
    return __cc_fpack(s, e, q);
}

uchar __cc_fnz(ulong a) {
    return (a & 0x7FFFFFFF) != 0;
}

uchar __cc_feq(ulong a, ulong b) {
    ulong ma = a & 0x7FFFFFFF;
    ulong mb = b & 0x7FFFFFFF;
    if (ma > 0x7F800000 || mb > 0x7F800000) {
        return 0;
    }
    if (ma == 0 && mb == 0) {
        return 1;
    }
    return a == b;
}

uchar __cc_flt(ulong a, ulong b) {
    ulong ma = a & 0x7FFFFFFF;
    ulong mb = b & 0x7FFFFFFF;
    if (ma > 0x7F800000 || mb > 0x7F800000) {
        return 0;
    }
    if (ma == 0 && mb == 0) {
        return 0;
    }
    if ((a ^ b) & 0x80000000) {
        return (a & 0x80000000) != 0;
    }
    if (a & 0x80000000) {
        return ma > mb;
    }
    return ma < mb;
}

uchar __cc_fle(ulong a, ulong b) {
    ulong ma = a & 0x7FFFFFFF;
    ulong mb = b & 0x7FFFFFFF;
    if (ma > 0x7F800000 || mb > 0x7F800000) {
        return 0;
    }
    if (ma == 0 && mb == 0) {
        return 1;
    }
    if ((a ^ b) & 0x80000000) {
        return (a & 0x80000000) != 0;
    }
    if (a & 0x80000000) {
        return ma >= mb;
    }
    return ma <= mb;
}

ulong __cc_ultof(ulong v) {
    int e = 150;
    if (v == 0) {
        return 0;
    }
    while (v & 0xFF000000) {
        v = v >> 1;
        e = e + 1;
    }
    while (!(v & 0x800000)) {
        v = v << 1;
        e = e - 1;
    }
    return __cc_fpack(0, e, v);
}

ulong __cc_ltof(long v) {
    if (v < 0) {
        return __cc_ultof((ulong)(0 - v)) | 0x80000000;
    }
    return __cc_ultof((ulong)v);
}

ulong __cc_utof(uint v) {
    return __cc_ultof(v);
}

ulong __cc_itof(int v) {
    return __cc_ltof(v);
}

ulong __cc_ftoul(ulong a) {
    int e = (int)((a >> 23) & 0xFF) - 127;
    ulong m;
    if ((a & 0x80000000) || e < 0) {
        return 0;
    }
    if (e >= 32) {
        return 0xFFFFFFFF;
    }
    m = (a & 0x7FFFFF) | 0x800000;
    if (e >= 23) {
        return m << (uchar)(e - 23);
    }
    return m >> (uchar)(23 - e);
}

long __cc_ftol(ulong a) {
    int e = (int)((a >> 23) & 0xFF) - 127;
    ulong m;
    ulong r;
    if (e < 0) {
        return 0;
    }
    if (e >= 31) {
        if (a & 0x80000000) {
            return (long)0x80000000;
        }
        return 0x7FFFFFFF;
    }
    m = (a & 0x7FFFFF) | 0x800000;
    if (e >= 23) {
        r = m << (uchar)(e - 23);
    } else {
        r = m >> (uchar)(23 - e);
    }
    if (a & 0x80000000) {
        return 0 - (long)r;
    }
    return (long)r;
}
