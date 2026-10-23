// C3: odwołanie do obiektu danych strumienia (&__stderr_f) bez wołania funkcji z file.c musi dołączyć moduł z jego definicją;
// plus snprintf(NULL, 0, ...) tylko liczy znaki i nic nie pisze na konsolę.
// expect-result: 2007
// expect-console: ok
#include <stdio.h>

FILE *log_stream = stderr;

int main(void)
{
    int need = snprintf(0, 0, "%d-%s", 12345, "abc");     /* 9, bez wyjścia */
    fprintf(stderr, "ok");
    return log_stream->kind * 1000 + need - 2;             /* 2000 + 9 - 2 */
}
