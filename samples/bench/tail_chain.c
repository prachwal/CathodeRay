uchar odd(uchar n);

uchar even(uchar n) {
    if (n == 0) return 1;
    return odd(n - 1);
}

uchar odd(uchar n) {
    if (n == 0) return 0;
    return even(n - 1);
}
