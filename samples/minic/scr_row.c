// scr_row.c — dokladnie pelny wiersz 0 (40 znaków, bez zawijania).
// Oczekiwane: wiersz 0 = 40x'A', reszta pusta.
void scr_putc(uchar c);
void scr_clear();
int main() {
    int i = 0;
    scr_clear();
    while (i < 40) { scr_putc(65); i = i + 1; }
    return 0;
}
