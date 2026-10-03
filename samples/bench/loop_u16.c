uint loop_u16(uint n) {
    uint s;
    s = 0;
    while (n) { s = s + n; n--; }
    return s;
}
