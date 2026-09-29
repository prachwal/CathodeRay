void copy(uchar *d, uchar *s, uint n) {
    while (n) { *d = *s; d++; s++; n--; }
}
