// scr_nl.c — nowe linie: 10 przechodzi na poczatek nastepnego wiersza.
// Oczekiwane: wiersz 0 = "A", wiersz 1 = "B", wiersz 2 pusty, wiersz 3 = "C".
void scr_putc(uchar c);
void scr_clear();
int main() {
    scr_clear();
    scr_putc(65);
    scr_putc(10);
    scr_putc(66);
    scr_putc(10);
    scr_putc(10);
    scr_putc(67);
    return 0;
}
