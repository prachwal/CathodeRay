// scr_goto.c — pozycjonowanie: rogi ekranu + ciecie poza zakres.
// Oczekiwane: buf[0]='A', buf[39]='B', buf[960]='C', buf[999]='E'
// (50,30) ciete do (39,24), wiec 'D' znika pod 'E'.
void scr_putc(uchar c);
void scr_clear();
void scr_goto(uchar x, uchar y);
int main() {
    scr_clear();
    scr_goto(0, 0); scr_putc(65);
    scr_goto(39, 0); scr_putc(66);
    scr_goto(0, 24); scr_putc(67);
    scr_goto(39, 24); scr_putc(68);
    scr_goto(50, 30); scr_putc(69);
    return 0;
}
