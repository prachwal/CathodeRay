// 07_control.c — do/while, switch (przechodzenie dalej, default), break/continue, enum.
// i = 3,6,9(pominięte),12,15,18, 21 przerywa: s = 3+6+12+15+18 = 54.
// Oczekiwane: A = 62 (54 + 10 - 1 - 1).
enum { STEP = 3, LIMIT = 20 };

int classify(uchar v) {
    switch (v) {
        case 1: return 10;
        case 2:
        case 3: return 20;
        default: return 1;
    }
}

int main() {
    int s = 0;
    int i = 0;
    do {
        i = i + STEP;
        if (i == 9) continue;
        if (i > LIMIT) break;
        s = s + i;
    } while (1);
    return s + classify(1) - classify(9) - 1;
}
