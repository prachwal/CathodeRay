// 11_goto_typedef.c — typedef, goto (pętla i wyjście), funkcja zwracająca wskaźnik.
// count(10) liczy i = 0, 3, 6, 9 (4 trafienia); *pick() = 20.
// Oczekiwane: A = 62 (4 * 10 + sizeof(word) = 2 + 20).
typedef uchar byte;
typedef int word;
byte data[3] = {10, 20, 30};

byte count(byte n) {
    byte i = 0;
    byte hits = 0;
top:
    if (i >= n) goto done;
    if (i % 3 == 0) hits++;
    i++;
    goto top;
done:
    return hits;
}

byte *pick() { return data + 1; }

int main() {
    word w = count(10);
    return w * 10 + sizeof(word) + *pick();
}
