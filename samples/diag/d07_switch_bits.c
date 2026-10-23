// switch i operacje bitowe na uchar/uint.
// expect-result: 1807
uint mix(uchar k, uint x)
{
    switch (k) {
    case 0: return x << 3;
    case 1: return x >> 2;
    case 2: return x ^ 0x00FF;
    case 3: return x | 0x0100;
    default: return x & 0x0F0F;
    }
}

int main(void)
{
    uint v = 5000;
    uchar k;
    for (k = 0; k < 5; k++) {
        v = mix(k, v);
    }
    return v;
}
