// 06_defines.c — preprocesor: #include wkleja plik, #define to stałe tekstowe.
// Wynik: 109.
#include "consts.inc"
#define STEP 3
#define LIMIT 9
int main() {
    int i = 0;
    while (i < LIMIT) i = i + STEP;   // 0,3,6,9 -> 9
    return i + BASE;                  // 109
}
