# Mini-C — opis języka

Mini-C to podzbiór C dla procesora stub (8-bitowy akumulator `A`, indeks `X`, stos na `A`). Kompilator:
`cathode cc plik.c [plik2.c] -o program.bin` (parser → checker → codegen → asembler → linker; biblioteka
standardowa linkowana automatycznie). Konwencje wołań, układ pamięci i szczegóły runtime: `stub-calling-conv.md`.

Przykłady poniżej z blokiem <code>```c expect=N</code> są **kompilowane i uruchamiane w testach** (`MiniCDocsTests`);
wynik `main` (młodsze 16 bitów, `A` + 256·`X`) musi równać się `N`. Blok <code>```c error="tekst"</code> musi
zakończyć się błędem kompilacji zawierającym tekst.

## Typy

| typ | rozmiar | uwagi |
| --- | --- | --- |
| `uchar` (`char`) | 1 B | bez znaku, 0..255 |
| `int` | 2 B | ze znakiem; porównania, `/`, `%`, `>>` ze znakiem |
| `uint` | 2 B | bez znaku |
| `T *`, `T (*f)(…)` | 2 B | wskaźnik do danych / funkcji |
| `T a[N]` | N·rozmiar | tablica jednowymiarowa, rozpada się na wskaźnik |
| `struct S` | suma pól | bez wyrównania |
| `void` | — | wynik funkcji, pusta lista parametrów `f(void)`, `void *` |

Działanie na dwóch `uchar` daje `uchar` (zawija się na 8 bitach), z `int`/`uint` — 16 bitów (`uint`, jeśli któryś jest `uint`).
Stałe do 255 mają typ `uchar`, większe `int`; działania na samych stałych liczy kompilator w 16 bitach (`1 << 15` to `int`).

```c expect=44
int main() {
    uchar small = 200;
    uchar wrapped = small + 100;      // uchar + uchar-stała: 8 bitów
    return wrapped;
}
```

```c expect=1200
int main() {
    uchar small = 200;
    int wide = small + 1000;          // stała > 255 jest int: 16 bitów
    return wide;
}
```

```c expect=1
int main() {
    int a = 0 - 1;
    uint b = 65535;
    return (a < 0) && (b > 1000);     // int ze znakiem, uint bez znaku
}
```

## Wyrażenia i operatory

Arytmetyka `+ - * / %`, bitowe `& | ^ ~ << >>`, porównania, `&& || !`, `?:`, przypisania złożone (`+=` … `>>=`),
`++`/`--` (przed i po), `&x`, `*p`, `p[i]`, `s.f`, `p->f`, `sizeof`. Działania na wskaźnikach skalują się rozmiarem elementu;
`p - q` daje liczbę elementów. Niejawne konwersje `uchar`↔`int`↔`uint` (z ostrzeżeniem przy zawężeniu); jawne rzutowania `(T)x` opisuje sekcja „Rzutowania”.

```c expect=75
int main() {
    int v = 5;
    int r = v++ * 10 + ++v;           // 5 * 10 + 7
    int *p = &r;
    *p += 18;                         // 57 + 18
    return r;
}
```

```c expect=34
int main() {
    int a[5] = {1, 2, 3, 4, 5};
    int *p = a;
    int *q = a + 4;
    p += 2;
    return (q - p) + a[p - a] * 10 + sizeof(a) / 2 * 2 - 8;   // 2 + 30 + 10 - 8
}
```

## Rzutowania

`(T)x` zamienia wartość skalarną na `uchar`, `int`, `uint`, wskaźnik `T *`, wskaźnik do funkcji `(R (*)(A, B))f` albo alias
`typedef`. Zawężenie do `uchar` obcina do młodszego bajtu (bez ostrzeżenia), rozszerzenie do słowa uzupełnia zerem (`uchar` jest
bez znaku), rzutowania `int`↔`uint`↔wskaźnik zmieniają tylko typ. Niejawna zamiana liczby na wskaźnik (i odwrotnie) jest błędem —
trzeba ją zapisać rzutowaniem. Rzutowanie na `struct` i z `void` jest błędem; `(void)x` odrzuca wartość.

```c expect=817
int add(int a, int b) { return a + b; }
int main() {
    int big = 1000;
    uchar low = (uchar)big;                          // 232
    int wide = (int)low + 300;                       // 532
    uint u = (uint)65535 + 2;                        // 1
    int (*fp)(int, int) = (int (*)(int, int))add;
    int r = fp(3, 4);                                // 7
    uchar *p = (uchar *)&big;
    return low + wide + u + r + (int)(p != 0) + (int)((uchar)300);
}
```

```c error="cannot convert int to uchar*"
int main() {
    uchar *p = 1000;                                 // bez rzutowania
    return 0;
}
```

## `void *`, `size_t`, `NULL`, `offsetof`

`void *` przenosi dowolny wskaźnik: niejawnie konwertuje się do i z `T *` (bez odrzucania `const`), ale nie wolno go dereferencjonować,
indeksować ani używać w arytmetyce. `<stddef.h>` daje `size_t` (`uint`), `NULL` (`((void *)0)`) i `offsetof(struct S, pole)` — stałą
czasu kompilacji. Funkcje pamięciowe biblioteki mają sygnatury `void *memcpy(void *, const void *, size_t)`,
`void *memset(void *, uchar, size_t)`, `int memcmp(const void *, const void *, size_t)`.

```c expect=1004
#include <stddef.h>
#include <string.h>
struct Pair { uchar tag; int value; };
int main() {
    int a[3] = {1000, 2, 3};
    int b[3];
    void *dst = b;
    memcpy(dst, a, sizeof(a));
    return b[0] + offsetof(struct Pair, value) + (int)sizeof(void *) + (dst != NULL);   // 1000 + 1 + 2 + 1
}
```

```c error="cannot dereference a void pointer"
int main() {
    int x = 1;
    void *p = &x;
    return *p;
}
```

## Struktury przez wartość

Struktura może być argumentem i wynikiem funkcji (także przez wskaźnik do funkcji). Argument to kopia: wołający podaje adres, a
funkcja kopiuje strukturę do własnej lokalnej, więc zmiana parametru nie rusza oryginału. Wynik wraca we wspólnym buforze `cc_retbuf`
(64 B, crt0), który wołający od razu kopiuje do własnej tymczasowej — dlatego struktura zwracana przez wartość ma najwyżej 64 B, a
rekurencja z wynikiem struktury działa (bufor nie leży w ramce). `f().pole`, `x = f(y)`, `f(g())` i porzucenie wyniku są dozwolone.

```c expect=1038
struct P { int a; int b; };
struct P swap(struct P p) { struct P r; r.a = p.b; r.b = p.a; return r; }
int main() {
    struct P p;
    p.a = 3;
    p.b = 20;
    struct P q = swap(p);
    return q.a * 50 + q.b + swap(q).a * 0 + p.a * 5 + swap(swap(p)).b;   // 1000 + 3 + 0 + 15 + 20
}
```

## Instrukcje

`if/else`, `while`, `do … while`, `for` (z deklaracją w inicjalizacji), `switch` (stałe `case`, przechodzenie dalej, `default`),
`break`, `continue`, `goto` + etykiety, `return`, bloki, deklaracje w dowolnym miejscu bloku.

```c expect=30
int main() {
    int s = 0;
    for (int i = 0; i < 10; i++) {
        switch (i) {
            case 2: continue;
            case 5:
            case 6: s += 10; break;
            default: s += 1;
        }
    }
    int k = 0;
again:
    k++;
    if (k < 3) goto again;
    return s + k;                     // s = 7 (default) + 20 (case 5, 6) = 27, k = 3
}
```

## Funkcje

Do 6 argumentów (`A`, `X`, `cc_arg2..6`). Zwracany jest skalar lub wskaźnik. Rekurencja działa (stos sprzętowy 256 B;
kompilator ostrzega o głębokości i odrzuca zbyt głębokie łańcuchy wołań). Prototypy z `...` (tylko deklaracje) mają
argumenty 16-bit. Wskaźniki do funkcji: `int (*f)(int)`, `typedef`, tablice, pola struktur.

```c expect=120
int fact(int n) { return n <= 1 ? 1 : n * fact(n - 1); }
int main() { return fact(5); }
```

```c expect=13
typedef int (*binop)(int, int);
int add(int a, int b) { return a + b; }
int mul(int a, int b) { return a * b; }
binop ops[2] = { add, mul };
int main() { return ops[0](3, 4) + ops[1](2, 3); }
```

## Zmienne

Globalne i lokalne; `static` (lokalna: jedna komórka na program; globalna/funkcja: widoczność w module), `extern` (definicja w
innym module), `const` (tylko odczyt, `const T *p`). Inicjalizatory: stałe (liczby, `sizeof`, `enum`, adresy, napisy) trafiają do
danych; niestałe globalne (`int a = f();`) są liczone przed `main`. Tablice i struktury: `{ … }` zagnieżdżone, `[]` z długością
z inicjalizatora, napis dla tablicy `uchar`.

```c expect=19
struct P { uchar x; int y; };
struct P pts[2] = { {1, 10}, {2, 20} };
int total = pts[0].y + pts[1].y;           // niestały inicjalizator: liczony przed main (30)
static int counter;
int bump() { counter++; return counter; }
int main() { bump(); bump(); return total / 3 + counter * 4 + pts[1].x - 1; }
```

```c error="assignment to const"
const int k = 5;
int main() { k = 6; return 0; }
```

## Struktury, `typedef`, `enum`

```c expect=63
enum { A = 3, B, C = 10 };
typedef struct Node { int v; struct Node *next; } Node;
Node nodes[2];
int main() {
    nodes[0].v = A; nodes[0].next = &nodes[1];
    nodes[1].v = B + C; nodes[1].next = 0;
    Node copy = nodes[1];                      // kopiowanie struktury
    int s = 0;
    for (Node *n = nodes; n; n = n->next) s += n->v;
    return s + copy.v * 3 + sizeof(Node);     // 17 + 42 + 4
}
```

Struktury nie są przekazywane ani zwracane przez wartość (użyj wskaźnika).

## Preprocesor

`#include "plik"`, `#include <plik>` (`--incdir` i biblioteka), `#define` (obiektowe i funkcyjne), `#undef`, `#if/#ifdef/#ifndef/#elif/#else/#endif`,
`#error`, `#pragma once`, `cc -D NAZWA[=wartość]`. Bez `#` i `##`.

```c expect=9
#define SQ(x) ((x) * (x))
#define LIMIT 3
#ifdef LIMIT
int main() { return SQ(LIMIT); }
#else
int main() { return 0; }
#endif
```

## Biblioteka standardowa

`<string.h>`, `<ctype.h>`, `<stdlib.h>`, `<stdio.h>` (konsola to bufor pamięci; `printf` do 5 argumentów, `%d %u %x %c %s %%`).
Linkowane są tylko użyte moduły.

```c expect=53
#include <string.h>
#include <stdlib.h>
uchar buf[16];
int main() {
    strcpy(buf, "abc");
    strcat(buf, "42");
    return strlen(buf) + atoi(buf + 3) + 6;   // 5 + 42 + 6
}
```

## Diagnostyka

Błędy mają `plik:linia`. Ostrzeżenia (nieużywane nazwy, brak `return`, martwy kod, przypisanie w warunku, zawężenie,
porównanie `int` z `uint`) drukuje `cc` na stderr; `-Werror` traktuje je jak błędy, `--no-opt` (`-O0`) wyłącza optymalizator.

## Nie działa (świadomie)

| brak | zamiast |
| --- | --- |
| `float`, `long`, `short`, `unsigned` | `int`, `uint` |
| `union`, pola bitowe, tablice wielowymiarowe | `struct`, jednowymiarowe z ręcznym indeksem |
| operator przecinka, wartości `enum` z `sizeof(struct …)` | osobne instrukcje, `#define` |
| `#`, `##` w makrach, `\x` w napisach | — |
| funkcja zwracająca wskaźnik do funkcji | `typedef` + parametr |

```c error="takes at most 6"
int f(int a, int b, int c, int d, int e, int g, int h) { return a; }
int main() { return 0; }
```

```c error="at most 64"
struct S { uchar a[100]; };
struct S f() { struct S s; return s; }
int main() { return 0; }
```
