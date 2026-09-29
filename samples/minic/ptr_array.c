// ptr_array.c — tablice i indeksowanie: deklaracje, zapis/odczyt,
// wskaźnik na element, arytmetyka. Oczekiwane: A = 80.
uchar buf[4];

int main() {
    uchar loc[3];
    buf[0] = 10;
    buf[3] = 40;        // ostatni element globalnej
    loc[0] = 1;
    loc[2] = 9;         // ostatni element lokalnej
    uchar *p = buf;     // rozpad na wskaźnik
    p = p + 1;          // +1 bajt (uchar*)
    *p = 20;            // buf[1] = 20
    int s = buf[0] + buf[1] + buf[3] + loc[0] + loc[2];
    return s;           // 10+20+40+1+9 = 80
}
