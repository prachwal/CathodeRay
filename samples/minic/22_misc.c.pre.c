typedef unsigned char uchar;
typedef unsigned int uint;
typedef unsigned long ulong;
// union, operator przecinka, sklejanie napisów, \x, # i ## w makrach, enum z sizeof(struct). Wynik: 1261.
#include <stdio.h>
#include <stddef.h>

#define STR(x) #x
#define GLUE(a, b) a##b

union Word { int whole; uchar bytes[2]; };
struct Rec { uchar tag; union Word w; uchar pad[2]; };
enum { REC_SIZE = sizeof(struct Rec), NEXT, WORD_AT = offsetof(struct Rec, w) };

int main() {
    struct Rec r;
    int i;
    int j;
    int steps = 0;
    for (i = 0, j = 9; i < j; i++, j--) {
        steps++;
    }
    r.tag = 1;
    r.w.whole = 0x0301;
    int GLUE(tot, al) = (steps = steps + 1, steps * 100);
    puts(STR(union  ok) "\x21");
    return total + REC_SIZE * 100 + NEXT * 10 + WORD_AT + r.tag * 100;
}
