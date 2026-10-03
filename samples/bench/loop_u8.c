void loop_u8(uchar *p, uchar n) {
    while (n) { *p = 0; p++; n--; }
}
