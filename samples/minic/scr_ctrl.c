// scr_ctrl.c — znaki niedrukowalne: NUL, BEL i >126 dekoder daje jako spacje.
// Oczekiwane: wiersz 0 = "A B" (0, 7 i 200 to spacje).
void scr_putc(uchar c);
void scr_clear();
int main() {
    scr_clear();
    scr_putc(65);
    scr_putc(0);
    scr_putc(66);
    scr_putc(7);
    scr_putc(200);
    return 0;
}
