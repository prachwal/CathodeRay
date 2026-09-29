uint sum_bytes(uchar *p, uchar n) {
    uint s;
    s = 0;
    while (n) { s = s + *p; p++; n--; }
    return s;
}
