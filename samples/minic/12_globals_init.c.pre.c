typedef unsigned char uchar;
typedef unsigned int uint;
typedef unsigned long ulong;
// 12_globals_init.c — inicjalizatory globalne: stałe (enum, sizeof), adres z przesunięciem
// i niestałe (wołają funkcję przed main, w kolejności deklaracji).
// a = 105, *mid = 2, n = 6, first = 7, second = 14 + 7 = 21, calls = 2.
// Oczekiwane: A = 143.
enum { BASE = 100 };
int a = BASE + 5;
int tab[3] = {1, 2, 3};
int *mid = tab + 1;
int n = sizeof(tab);
int calls;

int next() {
    calls++;
    return calls * 7;
}

int first = next();
int second = next() + first;

int main() {
    return a + *mid + n + first + second + calls;
}
