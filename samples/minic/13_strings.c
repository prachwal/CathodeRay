// 13_strings.c — biblioteka standardowa: napisy, ctype, itoa (linkowana przez cc).
// word = "mini-c": litery M I N I C dają 12 + 8 + 13 + 8 + 2 = 43, strlen = 6.
// Oczekiwane: A = 49 (43 + 6).
#include <string.h>
#include <ctype.h>
#include <stdlib.h>

uchar word[12];
uchar digits[8];

int main() {
    strcpy(word, "mini");
    strcat(word, "-c");
    int score = 0;
    for (uint i = 0; i < strlen(word); i++) {
        if (isalpha(word[i])) score += toupper(word[i]) - 'A';
    }
    itoa(score, digits, 10);
    if (strcmp(digits, "43") != 0) return 0;
    return atoi(digits) + strlen(word);
}
