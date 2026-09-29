// scr_full.c — caly ekran: dokladnie 1000 znaków (ostatnia komorka 999).
// Oczekiwane: 25 wierszy po 40x'C' (zadnego ciecia, zadnego przepełnienia).
void scr_putc(uchar c);
void scr_clear();
int main() {
    int i = 0;
    scr_clear();
    while (i < 1000) { scr_putc(67); i = i + 1; }
    return 0;
}
