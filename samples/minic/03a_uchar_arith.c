// 03a_uchar_arith.c — arytmetyka uchar: +, -, *, /, %, <<, >>, &, |, ^, ~,
// minus unarny. Operacje *,/,%,<<,>>,&,|,^ liczone w zmiennej uchar
// (int ich nie ma w v1). Suma = 1031, zwrot int: A = 7, X = 4.
int main() {
    uchar a = 200;
    uchar b = 100;
    uchar t = 0;
    int s = 0;
    s = s + (a + b);    // 44
    s = s + (a - b);    // 100
    t = a * 2; s = s + t;     // 144
    t = a / 3; s = s + t;     // 66
    t = a % 3; s = s + t;     // 2
    t = a << 1; s = s + t;    // 144
    t = a >> 2; s = s + t;    // 50
    t = a & 15; s = s + t;    // 8
    t = a | 15; s = s + t;    // 207
    t = a ^ 255; s = s + t;   // 55
    s = s + (~a);       // 55
    s = s + (-b);       // 156
    return s;           // 1031
}
