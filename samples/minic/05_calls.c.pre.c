typedef unsigned char uchar;
typedef unsigned int uint;
typedef unsigned long ulong;
// 05_calls.c — wołania: 0/1/2 argumenty, mix uchar/int, zagnieżdżenia,
// void, rekurencja, przypisanie złożone i przypisanie jako wyrażenie.
// Wynik: 400, zwrot int: A = 144, X = 1.
int glob = 7;

int add(int a, int b) { return a + b; }
uchar idu(uchar x) { return x; }
void setg(int v) { glob = v; }
int sum(int n) {
    if (n <= 0) return 0;
    return n + sum(n - 1);
}

int main() {
    int s = 0;
    s = add(10, 20);            // 30
    s = s + idu(5);             // 35 (uchar w wyrażeniu int)
    setg(100);                  // void
    s = s + glob;               // 135
    s = s + sum(5);             // 15 -> 150
    s = s + add(add(1, 2), 3);  // 6 -> 156 (zagnieżdżenie)
    int t = 0;
    t += 5;                     // 5
    t -= 2;                     // 3
    s = s + t;                  // 159
    uchar m = 3;
    m *= 4;                     // 12
    m /= 3;                     // 4
    m %= 3;                     // 1
    m <<= 3;                    // 8
    m >>= 1;                    // 4
    m &= 6;                     // 4
    m |= 1;                     // 5
    m ^= 7;                     // 2
    s = s + m;                  // 161
    uchar u = 0;
    u = (s = 200);              // przypisanie jako wyrażenie
    return s + u;               // 400
}
