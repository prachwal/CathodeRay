int sw_dense(uchar c) {
    switch (c) {
        case 0: return 1000;
        case 1: return 2000;
        case 2: return 3500;
        case 3: return 4100;
        case 4: return 5700;
        case 5: return 6300;
        case 6: return 7900;
        case 7: return 8200;
        default: return 0;
    }
}
