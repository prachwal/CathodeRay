typedef unsigned char uchar;
typedef unsigned int uint;
typedef unsigned long ulong;
// 10_struct.c — struct: pola, wskaźniki, tablica struktur z inicjalizatorem, kopiowanie, typedef.
// pool = {10, 20, 30}; kopia spare.qty = 31 -> pool[2].qty = 62; suma = 10 + 20 + 62 = 92.
// Oczekiwane: A = 97 (92 + sizeof(Item) = 5).
typedef struct Item {
    uchar id;
    int qty;
    struct Item *next;
} Item;

Item pool[3] = { {1, 10, 0}, {2, 20, 0}, {3, 30, 0} };

int total(Item *it) {
    int s = 0;
    while (it) {
        s += it->qty;
        it = it->next;
    }
    return s;
}

int main() {
    pool[0].next = &pool[1];
    pool[1].next = &pool[2];
    Item spare = pool[2];
    spare.qty++;
    pool[2].qty = spare.qty * 2;
    return total(pool) + sizeof(Item);
}
