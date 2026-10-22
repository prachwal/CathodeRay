#include <stdlib.h>

/* exit/abort: brak systemu, więc zatrzymanie to pętla w miejscu (emulatory w testach traktują ją jak HALT). */
void exit(int code) {
    while (1) {
    }
}

void abort() {
    exit(1);
}
