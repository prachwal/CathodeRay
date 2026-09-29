// 03b_uchar_cmp.c — porównania i logika uchar: ==, !=, <, <=, >, >=,
// &&, ||, !, ?:, w tym granice == dla nieostrych. Suma = 20, zwrot: A = 20.
int main() {
    uchar a = 200;
    uchar b = 100;
    int s = 0;
    s = s + (a == 200); // 1
    s = s + (a != b);   // 1
    s = s + (a > b);    // 1
    s = s + (b < a);    // 1
    s = s + (a >= 200); // 1
    s = s + (b <= 100); // 1
    s = s + (a && b);   // 1
    s = s + (a || b);   // 1
    s = s + (!a);       // 0
    s = s + (a > b ? 10 : 20);   // 10
    s = s + (b <= b);   // 1 (granica ==)
    s = s + (b >= b);   // 1 (granica ==)
    s = s + (b > b);    // 0 (granica ==)
    s = s + (b < b);    // 0 (granica ==)
    return s;           // 20
}
