// 15_funcptr.c — wskaźniki do funkcji: typedef, tablica struktur z callbackiem, wołanie pośrednie.
// apply('+', 6, 4) = 10, apply('*', 5, 3) = 15, apply('-', 9, 2) = 7.
// Oczekiwane: 1037 (10 * 100 + 15 * 2 + 7).
typedef int (*op_t)(int, int);

int add(int a, int b) { return a + b; }
int sub(int a, int b) { return a - b; }
int mul(int a, int b) { return a * b; }

struct Op {
    uchar symbol;
    op_t fn;
};

struct Op ops[3] = { {'+', add}, {'-', sub}, {'*', mul} };

int apply(uchar symbol, int a, int b) {
    for (uchar i = 0; i < 3; i++) {
        if (ops[i].symbol == symbol) return ops[i].fn(a, b);
    }
    return 0;
}

int main() {
    return apply('+', 6, 4) * 100 + apply('*', 5, 3) * 2 + apply('-', 9, 2);
}
