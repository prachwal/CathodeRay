#include <stdio.h>

/* fprintf: stdout i stderr to ta sama konsola (patrz printf), stdin nie jest strumieniem do zapisu. */
int fprintf(FILE *f, const uchar *fmt, int a1, int a2, int a3, int a4) {
    if (f == 0 || f->kind == 0) return -1;
    return printf(fmt, a1, a2, a3, a4, 0);
}
