// 02_control.c — sterowanie: if/else-if/else, while, for, zagnieżdżone bloki.
// Wynik: 42.
int main() {
    int s = 0;
    int i = 0;
    while (i < 5) {          // s = 0+1+2+3+4 = 10
        s = s + i;
        i = i + 1;
    }
    for (i = 0; i < 3; i = i + 1) s = s + 10;   // s = 40
    if (s == 40) s = s + 1; else s = 0;          // s = 41
    if (s > 100) s = 0;
    else if (s > 40) s = s + 1;                  // s = 42
    else s = 0;
    return s;
}
