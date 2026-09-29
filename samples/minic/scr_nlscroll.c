// scr_nlscroll.c — nowa linia na dole scrolluje: 'A' wjezdza na wiersz 23,
// 'B' laduje na wyczyszczonym 24. Bez scrolla 'A' zostaloby na dole.
// Oczekiwane: wiersz 23 = "A", wiersz 24 = "B", reszta pusta.
void scr_putc(uchar c);
void scr_clear();
void scr_goto(uchar x, uchar y);
int main() {
    scr_clear();
    scr_goto(0, 24);
    scr_putc(65);
    scr_putc(10);
    scr_putc(66);
    return 0;
}
