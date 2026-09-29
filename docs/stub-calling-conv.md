# Konwencja wołań stub (mini-C)

Cel: spisane zasady, żeby dwa podprogramy (i kiedyś kompilator) dogadywały się
bez zgadywania. Stub ma tylko `A`, `X` i stos na `A` — reszta to umowa.

## Rejestry

| Rejestr | Rola |
| --- | --- |
| `A` | wartość robocza, argument 1, wynik funkcji |
| `X` | licznik pętli, argument 2, indeks tablic (`,X`) |
| `C` | pożyczka/przeniesienie między operacjami (ulotne — gałąź je czyta) |
| `Z` | wynik ostatniego testu (ulotny) |
| `SP` | start `FF`, tylko `PUSH`/`POP` na `A`; calle zostawia zrównoważony |

## Funkcje

- 1\. argument w `A` (int: `A`=lo, `X`=hi), 2\. bajtowy przy bajtowym 1\. w `X`; pozostałe
  (int lub 3\.–6\.) w komórkach `cc_arg2`..`cc_arg6` (+`_h` dla int), max 6.
- Wynik w `A` (reszta dzielenia w `X`, jak `divmod`).
- Dwa `int` nie mieszczą się w (`A`, `X`): codegen woła z `A`=lo(arg1),
  `X`=hi(arg1), a arg2 odkłada do umówionych komórek `cc_arg2`/`cc_arg2_h`
  tuż przed `CALL` (po ewaluacji arg1 — bezpieczne przy zagnieżdżeniu);
  callee kopiuje je do swoich komórek w prologu, zanim cokolwiek zawoła.
- Komórki umówione (`cc_arg1`, `cc_arg2`, `cc_ret` + `_h`) i `__bss_start`
  definiuje crt0 (linkowane zawsze pierwsze); moduły C ich nie emitują.
- Caller: nic nie zachowuje (rejestry ulotne). Callee: nie rusza `SP`
  (poza własnymi `PUSH`/`POP` w parach) i odtwarza `X`, jeśli go używa
  niezgodnie z rolą (przez `PUSH`/`TXA` + odtworzenie).
- Brak ramki stosowej: stub nie adresuje względem `SP`, więc zmienne lokalne
  to zwykłe komórki absolutne (jak globalne). Rekurencja wymaga ręcznego
  odkładania stanu na stos.

## Ramki (plan 20): callee-saves na stosie sprzętowym

Decyzja (item 1): zamiast wirtualnego stosu w `X` — callee odkłada własne
komórki (parametry, lokale, tempy) na stos sprzętowy (`PUSH` po `A`, strona
`01xxh`) przy wejściu i odtwarza przy wyjściu. Globale współdzielone (bez
odkładania). Rekurencja działa do wyczerpania strony stosu (~256 B).

Kolejność prologu (krytyczna): wpierw schowek wejścia (`cc_arg1`/`cc_arg1_h`,
bo `LDA` przy `PUSH` niszczy `A`), potem `PUSH`e, potem `stor`y parametrów.
Epilog: wynik do `cc_ret`/`cc_ret_h`, `POP`y, odtworzenie `A`/`X`, `RET`.
`cc_arg2` (2. int-arg) i `cc_ret` żyją tylko bez `CALL` pomiędzy zapisem
a odczytem, więc zagnieżdżenie jest bezpieczne.

## I/O (plan 22): konsola jako stały adres

Decyzja (item 1): stub nie ma urządzeń, więc konsola to umowa testowa —
stały bufor `__io_buf` (256 B) + kursor `__io_cur` z `samples/stub/lib/io.s`.
`putchar` (A = znak) dopisuje i inkrementuje kursor; `puthex` (A = bajt)
dopisuje 2 znaki hex; `putdec` (A/X = int ze znakiem) dopisuje cyfry
dziesiętne (znak, dzielenie 16-bit przez odejmowanie jak `divmod`).
`samples/minic/lib/puts.c`: `puts(uchar *s)` na bufor (pętla ze wskaźnikiem;
`puts_at` celowo brak — wołanie ma max 2 argumenty, składa się
`scr_goto` + `puts`). Kolejność linkowania: crt0, lib, program (entry =
pierwsza linia CODE; zła kolejność wykonuje bibliotekę jako program).

## Ekran 40x25 (screen.s → Markdown)

` samples/stub/lib/screen.s`: bufor `__scr_buf` (1000 B, wierszami) + kursor
`__scr_cur`/`_h` (0..999). `scr_putc` (A = znak) dopisuje; 10 to nowa linia (wiersz+1, na dole scroll);
pełny ekran scrolluje w górę (ostatni wiersz zerowany, kursor na 960,
znaki sprzed scrolla przepadają), `scr_clear` zeruje bufor i kursor.
`scr_goto` (A = x 0..39, X = y 0..24, poza zakresem tnie) stawia kursor
na y*40+x (mnożenie shiftami, bez overflow: y ≤ 24, wynik ≤ 999).
Strony 256 B wybierane skokami
(X ma 8 bitów): osobne etykiety `__scr_buf`, `+256`, `+512`, `+768`.
Dekoder po stronie hosta (`cathode stub run … --screen-at ADDR
[--screen-size SxW] [--screen-out plik.md]`): obszar pamięci wierszami
(domyślnie 40x25) na tekst (0/niedrukowalne to spacja, końcowe spacje cięte),
plik `.md` to nagłówek + blok text. Adres wybiera dowolny bufor
(np. `--screen-at` na `__io_buf` + `--screen-size 16x2).`

## Wzorce codegen

```
if (a < b):      CPA b / BCS else  (C=1 znaczy a>=b; uwaga: CPA tylko d8!)
while (x > 0):   loop: ... / DEX / BNE loop
for (i=0;i<n):   LDX 0 / loop: ... / INX / CPX n / BNE loop
f(g(x)):         CALL g / STA tmp / ... / CALL f   (wynik przez tmp)
tab[i]:          LDA tab,X / STA tab,X
16-bit a+b:      LDX 0 / CLC / LDA al / ADD bl,X / STA rl / LDA ah / ADC bh,X / STA rh
```

Ograniczenia stałe: `CPA`/`CPX`/`ADD`/`SUB` biorą tylko stałe `d8`
(porównanie zmienna-ze-zmienną: tylko przez łatanie operandu w kodzie,
wzorzec `divmod` z mathlib.s), skoki warunkowe tylko od `Z`/`C`,
brak `ADD`/`SUB` z pamięci absolutnej (tryb `,X` z `X = 0`).

## Rozszerzenia mini-C (plan 26)

- Literały: `'a'`, `'\n'`, `'\0'` (uchar); `"tekst"` to `uchar*` na bajty w DATA + `0`
  (deduplikowane w module; inicjalizator globala napisem nieobsługiwany).
- `++`/`--` (przed/po): `x++` to `(x = x + 1) - 1`, w pozycji instrukcji samo przypisanie.
- `break`/`continue` (najbliższa pętla; w `for` continue skacze do kroku).
- `int`: `& | ^ << >> * / %` (16-bit **ze znakiem**: porównania, `/` `%` do zera, `>>` arytmetyczne; dzielenie przez 0 daje 0/0);
  `cc_mul16`/`cc_div16` emitowane lokalnie w module (bez `.global`), komórki `cc_w_*`.
- Działania na dwóch stałych składa parser (16-bit z zawijaniem): `1 << 15`, `200 + 100`, `-1` to `int`.
- Błędy typów/codegenu mają `plik:linia` (`cc`).
- `for (int i = ...; ...)` działa (wcześniej deklaracja w init łamała parser).

## Rozszerzenia mini-C (plan 27 B)

- `do … while`, `switch` (stałe `case`, przechodzenie dalej jak w C, `default`, `break`; wartość
  w parze komórek `__swN`, `continue` przechodzi do zewnętrznej pętli).
- `enum { A, B = 5 };` (stałe podstawiane przez parser), `sizeof(typ)`, `sizeof x` (tablice też), `char` = `uchar`.
- `*p += x`, `p[i] -= x`, `p += n`, `x[i]++` (cel liczony dwa razy, więc bez `++`/wywołań w celu).
- Tablice: `uchar a[] = {1,2}`, `int w[4] = {1000}`, `uchar s[] = "hi"`; brakujące elementy zerowane
  (lokalne przy każdym wejściu). Długość `[N]` może być stałym wyrażeniem (`enum`, `sizeof`).
- Globalne wskaźniki z napisem, `&g` albo nazwą tablicy: komórka `.word` (starszy bajt to `nazwa+1`).
- Globalne wskaźniki i funkcje: `int *g;` działa (wcześniej gwiazdka po nazwie); funkcje zwracające wskaźnik nie.

## Runtime (plan 27 C)

- Układ pamięci `cc`: CODE `$1000` (12 KB), BSS `$4000` (4 KB), DATA `$5000`. crt0 zeruje BSS od `__bss_start`
  do `__bss_end` (linker dodaje `__<segment>_end`, w jednym pliku daje go codegen), więc BSS nie ma już limitu 256 B.
- `cc_mul8`/`cc_divmod` (uchar) to shift-add / dzielenie pisemne w 8 krokach, lokalne w module (bez `.global`).
- Stos to strona `$01xx` (256 B): łańcuch wołań głębszy niż 256 B (ramka = PUSHe + 2 B adresu) to błąd
  kompilacji; rekurencja daje ostrzeżenie z szacunkiem głębokości (`cc` drukuje ostrzeżenia na stderr).

## Rozszerzenia mini-C (plan 28)

- `typedef` (aliasy typów i wskaźników), `goto` + etykiety (w obrębie funkcji), funkcje zwracające
  wskaźnik (`uchar *f()`, wynik jak `int`: A=lo, X=hi), porównania wskaźników (bez znaku) i `p == 0`.
- Złożone przypisania i `++`/`--` przez wskaźnik liczą adres celu raz (`a[i++] += 1`, `*p++ -= 1`):
  ukryty wskaźnik `__aoN` (chroniony ramką).
- Globalne inicjalizatory: stałe (liczby, `sizeof`, działania, `enum`) trafiają do DATA; adresy
  (`&g`, napis, tablica, `tab + 2`) do komórki `.word`; reszta (np. `int a = f();`) jest liczona
  w `__cc_init` modułu, wpisanym do tablicy segmentu INIT (`.word`). Crt0 po zerowaniu BSS woła
  każdy wpis tablicy (`__init_start`..`__init_end`, symbole linkera `__<segment>_start/_end`).
- Układ `cc`: CODE `$1000` (do `$3EFF`), INIT `$3F00` (256 B = 128 modułów), BSS `$4000`, DATA `$5000`.

## struct (plan 28 D)

- `struct S { pola };`, `struct S x;`, `typedef struct [S] { … } T;`, `struct S *p`, tablice struktur,
  zagnieżdżone struktury i pola-tablice, `sizeof(struct S)`/`sizeof x`. Pola leżą kolejno, bez wyrównania.
- Dostęp `s.f`, `p->f`, `a[i].f`, `&s.f`, `&a[i]`; `++`, `+=` itd. na polach (adres liczony raz).
  Arytmetyka wskaźników skaluje rozmiarem struktury (`cc_mul16` dla rozmiarów innych niż 1 i 2).
- Kopiowanie `a = b`, `*p = a`, `arr[i] = a`, `struct S t = s;` pętlą bajtów (do 255 B).
  Przekazywanie i zwrot struktury **przez wartość** jest błędem typów (użyj wskaźnika).
- Inicjalizatory `{a, b}` z zagnieżdżeniem (`{ {1,2}, "ab", 0 }`), brakujące pola zerowane;
  globalne muszą być stałe (bajty w DATA), lokalne mogą być wyrażeniami.
- Ograniczenia: brak przekazywania struktur przez wartość.

## Plan 29 A: stałe, wskaźniki, rekurencja

- Checker liczy stałe wyrażenia (`sizeof` typu/zmiennej/wyrażenia/struktury, działania, porównania) do `Constants`;
  stała <= 255 ma typ `uchar`, wyżej `int`. Używają jej długości tablic, `case`, inicjalizatory globalne i codegen.
- `p - q` (ten sam typ wskaźnika) daje `int` w elementach; `int - ptr` nadal błąd.
- Globalne tablice i struktury mogą mieć w środku adresy (`&g`, `&a[1]`, `&s.f`, napisy, `tab + 2`): dane wychodzą
  jako mieszanka `.byte` i `.word symbol`.
- Lokalne tablice i struktury funkcji rekurencyjnych (także wzajemnie) są zapisywane na stosie bajt po bajcie
  (limit 64 B na obiekt); w pozostałych funkcjach nie kosztują nic.
- Kopiowanie struktur i lokalne inicjalizatory nie mają już limitu 255/256 B.

## Preprocesor (plan 29 B)

- `#include "f"` (katalog pliku, `.`, `--incdir`), `#include <f>` (tylko `--incdir` i biblioteka standardowa),
  `#pragma once`, osłony `#ifndef X` / `#define X`.
- `#define NAZWA wartość`, `#define F(a, b) ciało` (rozwijanie tokenowe: napisy, znaki i komentarze zostają,
  makro nie rozwija się we własnym ciele), `#undef`, kontynuacja linii `\`, `cc -D NAZWA[=wartość]`.
- `#if`/`#elif` (stałe całkowite, `defined`, makra, `? :`, `&& ||`, nieznane nazwy = 0), `#ifdef`, `#ifndef`, `#else`, `#endif`,
  `#error`. Dyrektywy zostawiają puste linie (numeracja linii bez zmian; `#include` wkleja plik).
- Bez `#` (napis) i `##` (sklejanie).

## static, extern, const (plan 29 C)

- `static` zmienna lokalna: jedna komórka na cały program (DATA/BSS pod etykietą `funkcja__nazwa`, bez ramki, nie
  zapisywana przy rekurencji), inicjalizator musi być stały i jest liczony raz. `static` globalna i funkcja:
  symbol lokalny modułu (bez `.global`), więc dwa moduły mogą mieć własne `count` i `helper`.
- `extern T x;` bez miejsca (`.extern cc_g_x`); starszy bajt globala to zawsze `cc_g_x+1` (komórki lo/hi leżą obok
  siebie), więc `extern int`, tablice, struktury i wskaźniki działają między modułami. `extern` po definicji w tym
  samym pliku jest dozwolone, `extern` z inicjalizatorem lub lokalny — błąd.
- `const`: `const T x`, `T const x`, `const T *p` (pełna kontrola: zapis przez wskaźnik do const, `discards const`
  przy przypisaniu `const T*` do `T*`), `const` w polach i strukturach. `T * const p` jest parsowane, ale
  wskaźnik pozostaje zapisywalny. Dane const nie trafiają do osobnej pamięci (RAM bez ochrony).
