uint str_len(uchar *s) {
    uint n;
    n = 0;
    while (*s) { n++; s++; }
    return n;
}
