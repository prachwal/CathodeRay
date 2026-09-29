// puts.c — biblioteka C: puts na bufor __io_buf (przez putchar z io.s).
// Wymaga wskaźników (plan 24): s[i] i pętla. puts_at celowo brak —
// wołanie ma max 2 argumenty (konwencja), więc składa się scr_goto + puts.
// Linkowana jak zwykły moduł .c (prototyp w wołającym, definicja tu).
void putchar(uchar c);

void puts(uchar *s) {
    int i = 0;
    while (s[i] != 0) {
        putchar(s[i]);
        i = i + 1;
    }
}
