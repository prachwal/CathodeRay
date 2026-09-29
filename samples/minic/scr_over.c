// scr_over.c — przepełnienie scrolluje: 1000x'A' + 5x'B'.
// Oczekiwane: wiersze 0..23 pełne 'A', wiersz 24 = "BBBBB" + 35x'A'.
// (Bez scrolla byłoby samo 'A' — tak odróżniamy scroll od gubienia.)
void scr_putc(uchar c);
void scr_clear();
int main() {
    int i = 0;
    scr_clear();
    while (i < 1000) { scr_putc(65); i = i + 1; }
    i = 0;
    while (i < 5) { scr_putc(66); i = i + 1; }
    return 0;
}
