int arith16(int a, int b) {
    if (a < b) return b - a;
    if (a > 100) return a + b - 100;
    return a - b;
}
