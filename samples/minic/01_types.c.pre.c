typedef unsigned char uchar;
typedef unsigned int uint;
typedef unsigned long ulong;
// 01_types.c — typy, parametry, globale, zwroty.
// Pokrywa: uchar/int/void, 0/1/2 parametry, globale z/bez init,
// zwrot wartosci, zawężenie int->uchar, wołanie void.
int g = 300;   // global z init
uchar b;       // global bez init (crt0 zeruje)

int add2(int a, int b) { return a + b; }
uchar low(int v) { return v; }   // zawężenie: 300 -> 44
void touch() { b = 1; }

int main() {
    int x = add2(100, 20);   // 120
    uchar y = low(300);       // 44
    touch();                  // b = 1
    return x + y + b + g - 300;   // 120+44+1+300-300 = 165
}
