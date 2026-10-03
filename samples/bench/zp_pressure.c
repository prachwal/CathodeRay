uchar g0;
uchar g1;
uchar g2;
uchar g3;
uchar g4;
uchar g5;
uchar g6;
uchar g7;
uchar g8;
uchar g9;
uchar g10;
uchar g11;
uchar g12;
uchar g13;
uchar g14;
uchar g15;

uchar zp_pressure(uchar x) {
    g0 = x;
    g1 = g0 + 1;
    g2 = g1 + 2;
    g3 = g2 + 3;
    g4 = g3 + 4;
    g5 = g4 + 5;
    g6 = g5 + 6;
    g7 = g6 + 7;
    g8 = g7 + 8;
    g9 = g8 + 9;
    g10 = g9 + 10;
    g11 = g10 + 11;
    g12 = g11 + 12;
    g13 = g12 + 13;
    g14 = g13 + 14;
    g15 = g14 + 15;
    return g15 + g0;
}
