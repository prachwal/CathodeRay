int twice(int x) { return x + x; }
int neg(int x) { return -x; }
int apply(int (*f)(int), int v) { return f(v); }
int pick(uchar k, int v) { return k ? apply(twice, v) : apply(neg, v); }
