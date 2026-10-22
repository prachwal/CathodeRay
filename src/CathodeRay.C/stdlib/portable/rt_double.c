/* double (-D CC_DOUBLE64): 64-bitowa pamięć, działania na float. Konwersje przesuwają wykładnik i mantysę;
   mantysa jest obcinana do 23 bitów, zero/denormale to zero, wykładniki poza zakresem dają nieskończoność albo zero. */
ulong __cc_dtof(unsigned long long *p) {
    unsigned long long d = *p;
    ulong sign = (ulong)(d >> 32) & 0x80000000;
    int e = (int)((d >> 52) & 0x7FF);
    ulong m = (ulong)((d >> 29) & 0x7FFFFF);
    if (e == 0) {
        return sign;
    }
    if (e == 0x7FF) {
        if (m || (d & 0x1FFFFFFF)) {
            m = m | 0x400000;
        }
        return sign | 0x7F800000 | m;
    }
    e = e - 1023 + 127;
    if (e >= 255) {
        return sign | 0x7F800000;
    }
    if (e <= 0) {
        return sign;
    }
    return sign | ((ulong)e << 23) | m;
}

void __cc_ftod(ulong a, unsigned long long *out) {
    unsigned long long sign = (unsigned long long)(a & 0x80000000) << 32;
    int e = (int)((a >> 23) & 0xFF);
    unsigned long long m = a & 0x7FFFFF;
    if (e == 0) {
        *out = sign;
    } else if (e == 255) {
        *out = sign | 0x7FF0000000000000 | (m << 29);
    } else {
        *out = sign | ((unsigned long long)(e - 127 + 1023) << 52) | (m << 29);
    }
}
