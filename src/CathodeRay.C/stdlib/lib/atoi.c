#include <stdlib.h>
#include <ctype.h>

int atoi(const uchar *s) {
    while (isspace(*s)) s++;
    int negative = 0;
    if (*s == '-') {
        negative = 1;
        s++;
    } else if (*s == '+') {
        s++;
    }
    int value = 0;
    while (isdigit(*s)) {
        value = value * 10 + (*s - '0');
        s++;
    }
    return negative ? 0 - value : value;
}
