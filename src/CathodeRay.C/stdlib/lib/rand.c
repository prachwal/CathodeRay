#include <stdlib.h>

static uint state = 44257;

void srand(uint seed) { state = seed ? seed : 1; }

uint rand() {
    state ^= state << 7;
    state ^= state >> 9;
    state ^= state << 8;
    return state;
}
