// Rzutowania: zawężenie, rozszerzenie, wskaźniki, wskaźnik do funkcji. Wynik: 817.
int add(int a, int b) { return a + b; }

int main() {
    int big = 1000;
    uchar low = (uchar)big;
    int wide = (int)low + 300;
    uint u = (uint)65535 + 2;
    int (*fp)(int, int) = (int (*)(int, int))add;
    int r = fp(3, 4);
    uchar *p = (uchar *)&big;
    return low + wide + u + r + (int)(p != 0) + (int)((uchar)300);
}
