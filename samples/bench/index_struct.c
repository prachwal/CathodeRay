struct P {
    uchar x;
    uchar y;
    uint w;
};

uint index_struct(struct P *a, uchar n) {
    uint s;
    uchar i;
    s = 0;
    for (i = 0; i < n; i++) s = s + a[i].w + a[i].x;
    return s;
}
