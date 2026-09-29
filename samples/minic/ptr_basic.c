// ptr_basic.c — adres i dereferencja: &x, *p odczyt/zapis, null.
// Oczekiwane: A = 84.
int main() {
    int x = 0;
    int *p = &x;    // adres
    *p = 42;        // zapis przez wskaźnik
    int y = *p;     // odczyt (42)
    int *q = 0;     // null
    q = p;          // kopiowanie wskaźników
    return y + *q;  // 84
}
