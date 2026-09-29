// 04_int_ops.c — operatory int: +, -, porównania, logika, ?:, negacja,
// konwersje uchar<->int. Suma = 1540, zwrot: A = 4, X = 6.
int main() {
    int a = 1000;
    int b = 250;
    int s = 0;
    s = s + (a + b);       // 1250
    s = s + (a - b);       // 750
    s = s + (a == 1000);   // 1
    s = s + (a != b);      // 1
    s = s + (a > b);       // 1
    s = s + (b < a);       // 1
    s = s + (a >= 1000);   // 1
    s = s + (b <= 250);    // 1
    s = s + (a && b);      // 1
    s = s + (b || a);      // 1
    s = s + (!a);          // 0
    s = s + (a > b ? 100 : 200);   // 100
    s = s + (-b);          // -250
    s = s + (b - a);       // -750
    uchar c = 200;
    s = s + c;             // int + uchar
    c = a;                 // zawężenie: 1000 -> 232
    s = s + c;
    return s;              // 1540
}
