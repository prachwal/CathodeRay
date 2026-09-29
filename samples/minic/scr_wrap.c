// scr_wrap.c — 41 znaków: granica wiersza 0/1 (40 pelny + 1 w nastepnym).
// Oczekiwane: wiersz 0 = 40x'B', wiersz 1 = "B", reszta pusta.
void scr_putc(uchar c);
void scr_clear();
int main() {
    int i = 0;
    scr_clear();
    while (i < 41) { scr_putc(66); i = i + 1; }
    return 0;
}
