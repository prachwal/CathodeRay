// scr_over.c — przepełnienie: 1005 znaków, 5 nadmiarowych gubi scr_putc.
// Oczekiwane: jak scr_full (25 wierszy po 40x'D', kursor stoi na 1000).
void scr_putc(uchar c);
void scr_clear();
int main() {
    int i = 0;
    scr_clear();
    while (i < 1005) { scr_putc(68); i = i + 1; }
    return 0;
}
