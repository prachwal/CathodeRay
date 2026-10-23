// Biblioteka standardowa: --verbose raportuje dołączone moduły, a zrzut IR zawiera tylko ten plik.
// expect-result: 5
// expect-verbose: linking stdlib module
// expect-ir-absent: func memset
#include <string.h>

uchar buf[8];

int main(void)
{
    memset(buf, 7, 8);
    return strlen("hello") + buf[3] - 7;
}
