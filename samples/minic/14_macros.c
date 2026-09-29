// 14_macros.c — preprocesor: makra funkcyjne, #if/#else, defined.
// MAX(3, SQUARE(4)) = 16, BASE = 100.
// Oczekiwane: A = 116.
#define MAX(a, b) ((a) > (b) ? (a) : (b))
#define SQUARE(x) ((x) * (x))
#define DEBUG 1

#if DEBUG && !defined(NDEBUG)
#define BASE 100
#else
#define BASE 0
#endif

int main() {
    int x = MAX(3, SQUARE(4));
    return x + BASE;
}
