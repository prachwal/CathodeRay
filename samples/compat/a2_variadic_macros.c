// A2: makra wariadyczne (`...` i `__VA_ARGS__`) w stylu C99: zero, jeden i wiele argumentów, zagnieżdżenia i #__VA_ARGS__.
// expect-result: 243
#include <string.h>

int sum3(int a, int b, int c) { return a + b + c; }
int first(int a) { return a; }

#define CALL(f, ...) f(__VA_ARGS__)
#define SUM(...) sum3(__VA_ARGS__)
#define PAIR(n, ...) (n * 10 + sum3(__VA_ARGS__))
#define LEN(...) strlen(#__VA_ARGS__)
#define NOARGS(x, ...) (x + 100 __VA_ARGS__)

int main(void)
{
    int a = CALL(first, 7);
    int b = SUM(1, 2, 3);
    int c = PAIR(2, 4, 5, 6) - 20;
    int d = NOARGS(5);
    int e = NOARGS(5, + 1);
    return a + b + c + d + e + LEN(a, b);
}
