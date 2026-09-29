// scr_empty.c — pusty ekran: sam scr_clear.
// Oczekiwane: 25 pustych wierszy (ramka pelna, tekstu brak).
void scr_putc(uchar c);
void scr_clear();
int main() {
    scr_clear();
    return 0;
}
