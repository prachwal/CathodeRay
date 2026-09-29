// 08_int_signed.c — int 16-bit ze znakiem: porównania, / % (do zera), >> arytmetyczne, *.
// a = -300, b = 7: q = -42, r = -6, m = -2100, sh = -75.
// Oczekiwane: A = 10 (3 trafienia + 6 + 1).
int main() {
    int a = 0 - 300;
    int b = 7;
    int q = a / b;
    int r = a % b;
    int m = a * b;
    int sh = a >> 2;
    int ok = 0;
    if (a < 0) ok = ok + 1;
    if (q == 0 - 42) ok = ok + 1;
    if (sh == 0 - 75) ok = ok + 1;
    if (m == 0 - 2100) ok = ok + 1;
    return ok + (0 - r) + 0;
}
