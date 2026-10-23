#include <stdio.h>

/* FILE nad konsolą: stdout i stderr dopisują do __io_buf (putchar), stdin czyta bufor wejściowy hosta (getchar) z jednym znakiem
   ungetc. Brak systemu plików: fopen zwraca NULL, fclose/fflush nic nie robią. */
FILE __stdin_f = {0, -1};
FILE __stdout_f = {1, -1};
FILE __stderr_f = {2, -1};

FILE *fopen(const uchar *path, const uchar *mode) {
    return 0;
}

int fclose(FILE *f) {
    return 0;
}

int fflush(FILE *f) {
    return 0;
}

int fputc(int c, FILE *f) {
    if (f == 0 || f->kind == 0) return EOF;
    putchar(c);
    return c & 255;
}

int putc(int c, FILE *f) {
    return fputc(c, f);
}

int fputs(const uchar *s, FILE *f) {
    while (*s) {
        if (fputc(*s, f) == EOF) return EOF;
        s++;
    }
    return 0;
}

int fgetc(FILE *f) {
    int c;
    if (f == 0 || f->kind != 0) return EOF;
    if (f->unget >= 0) {
        c = f->unget;
        f->unget = -1;
        return c;
    }
    return getchar();
}

int getc(FILE *f) {
    return fgetc(f);
}

int ungetc(int c, FILE *f) {
    if (c == EOF || f == 0 || f->kind != 0 || f->unget >= 0) return EOF;
    f->unget = c & 255;
    return c & 255;
}
