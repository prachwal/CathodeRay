// C3: <stdio.h> — FILE, stdin/stdout/stderr, fputc/fputs/fprintf/snprintf/getc/ungetc/fopen (bez systemu plików: NULL).
// stdout i stderr trafiają na konsolę; stdin to pusty bufor wejściowy, więc po ungetc zostaje jeden znak, potem EOF.
// expect-result: 112
// expect-console: abc-42-Eff12345Qe
#include <stdio.h>

int main(void)
{
    uchar buf[8];
    FILE *none;
    int n;

    fputs("ab", stdout);
    fputc('c', stdout);
    fprintf(stdout, "-%d-", 42);
    fprintf(stderr, "E%x", 255);
    n = snprintf(buf, 6, "%d-%s", 12345, "abcdef");    /* "12345" obcięte do 5 znaków, wynik = pełna długość 12 */
    fputs(buf, stdout);
    none = fopen("plik.txt", "r");
    ungetc('Q', stdin);
    fputc(getc(stdin), stdout);
    fputc(getc(stdin) == EOF ? 'e' : 'n', stdout);
    if (none == 0 && fclose(stdout) == 0) {
        return n + 100;
    }
    return 0;
}
