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
| `void` | — | tylko wynik funkcji |

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
`p - q` daje liczbę elementów. Niejawne konwersje `uchar`↔`int`↔`uint` (z ostrzeżeniem przy zawężeniu); bez rzutowań.

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
| rzutowania `(T)x` | niejawne konwersje |
| `union`, pola bitowe, tablice wielowymiarowe | `struct`, jednowymiarowe z ręcznym indeksem |
| `void *` | `uchar *` |
| operator przecinka, wartości `enum` z `sizeof(struct …)` | osobne instrukcje, `#define` |
| struktura przez wartość (argument, wynik) | wskaźnik |
| `#`, `##` w makrach, `\x` w napisach | — |
| funkcja zwracająca wskaźnik do funkcji | `typedef` + parametr |

```c error="takes at most 6"
int f(int a, int b, int c, int d, int e, int g, int h) { return a; }
int main() { return 0; }
```

```c error="by value"
struct S { uchar a; };
struct S f() { struct S s; return s; }
int main() { return 0; }
```
