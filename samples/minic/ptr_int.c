// ptr_int.c — wskaźniki int: skala x2 w arytmetyce i indeksie.
// g = [100, 200, 300]; p = g + 1 -> element 1 (200).
// Oczekiwane: A = 100 (200 + 300 - 400).
int g[3];

int at(int *p, int i) { return p[i]; }

int main() {
    g[0] = 100;
    g[1] = 200;
    g[2] = 300;
    int *p = g;
    p = p + 1;          // +2 bajty (int*)
    int a = *p;         // 200
    int b = at(g, 2);   // 300 przez indeks z parametru
    return a + b - 400; // 100
}
