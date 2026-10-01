# Konwencja wołań stub (mini-C)

Opis języka: [minic.md](minic.md) (przykłady testowane). Ten plik to konwencje, układ pamięci i decyzje projektowe.

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

- Układ pamięci `cc`: CODE `$1000` (24 KB), BSS `$7000` (4 KB), DATA `$8000`. crt0 zeruje BSS od `__bss_start`
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
- Układ `cc`: CODE `$1000` (do `$6EFF`), INIT `$6F00` (256 B = 128 modułów), BSS `$7000`, DATA `$8000`.

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

## Wskaźniki do funkcji (plan 29 C)

- Deklaracje `int (*op)(int, int)`, `typedef int (*binop)(int, int);`, tablice `binop tab[3] = { add, sub, mul }`,
  pola struktur, parametry i globalne z inicjalizatorem (`.word etykieta`). Nazwa funkcji i `&f` to adres.
- Wołanie `op(x, y)`, `(*op)(x, y)`, `tab[i](x)`, `s.cb(x)`, `p->cb(x)`: argumenty jak w zwykłym wywołaniu
  (A, X, `cc_arg2..6`), adres wpisywany w operand `CALL` tuż przed skokiem. Porównania i `if (f)` działają, arytmetyka nie.
- Wołanie pośrednie nie trafia do grafu wołań: rekurencja przez wskaźnik nie jest wykrywana (lokalne tablice takiej
  funkcji nie są zapisywane na stosie), a kontrola stosu jej nie zna.

## uint (plan 29 C)

- `uint` to 16-bit bez znaku: porównania, `/`, `%`, `>>` i mnożenie bez znaku; `int` op `uint` daje `uint`.
  Konwersje `int`/`uchar`/`uint` są niejawne (bez zmiany bitów). Porównanie `int` z `uint` (oba nie stałe) ostrzega.

## Biblioteka standardowa (plan 29 D)

Osadzona w `CathodeRay.C` (`stdlib/include/*.h`, `stdlib/lib/*.c`, konsola `io.s` z `samples/stub/lib`).
`#include <string.h>` itd. szuka najpierw w `--incdir`, potem w bibliotece. `cc` linkuje wyłącznie moduły definiujące
nierozwiązane symbole (także moduły biblioteki między sobą); funkcja zdefiniowana przez użytkownika wygrywa;
`--nostdlib` wyłącza auto-linkowanie.

| nagłówek | funkcje |
| --- | --- |
| `string.h` | `strlen strcpy strncpy strcat strcmp strncmp strchr memcpy memset memcmp` (`uchar *` zamiast `char`/`void *`, `strchr` bierze `uchar *`) |
| `ctype.h` | `isdigit isalpha isalnum isspace isupper islower toupper tolower` |
| `stdlib.h` | `abs min max atoi itoa(v, buf, base) srand rand` (xorshift 16-bit) |
| `stdio.h` | `putchar puthex putdec putstr puts printf sprintf` (`%d %u %x %c %s %%`, do 5 / 4 argumentów) |

Wariadyczne prototypy (`...`): dodatkowe argumenty są zawsze 16-bit w kolejnych komórkach `cc_argN`; definicja ma stałą
liczbę parametrów (`printf(fmt, a1..a5)`), więc `...` można tylko deklarować. Konsola to bufor `__io_buf` (256 B).

## Cele i kod pośredni (plan 30, kroki 1–2)

- `cc --cpu <nazwa>` wybiera cel z rejestru `CTargets` (dziś tylko `stub`; `6502`, `65c02`, `z80`, `8080`, `6800` są
  zapowiedziane i dają komunikat „target not implemented yet”). Cel (`ICTarget`) dostarcza crt0, moduły
  asemblerowe biblioteki (`io.s`), domyślny układ pamięci, limit stosu i drukuje kod pośredni jako asembler
  swojego CPU (`Emit`). Front-end C nie zależy od asemblera: układ pamięci to dane (`TargetLayout`), a `cc` zamienia je na `LinkerConfig`.
- Front-end (`Lowering`) obniża program do kodu pośredniego `Ir.Module`: funkcje (`Ir.Function`: parametry, szerokość
  wyniku, zapisywana ramka, instrukcje), dane (`Ir.Data` z typowanymi fragmentami: bajty i słowa z adresem symbolu) i
  deklaracje zewnętrzne. Instrukcje to trójadresowe operacje na komórkach (`Cell` = symbol + szerokość 1/2, `Imm`,
  `AddrOf`): `Mov`, `Bin`, `Un`, `Load`, `Store`, `CopyBlock`, `Fill`, `BrCmp` (porównanie i skok razem, warianty ze znakiem
  i bez), `Jmp`, `Label`, `Call`, `Ret`, `Src`. Operandy węższe od wyniku są rozszerzane zerem. Front-end nie zna flag,
  konwencji wołań, rozmieszczenia komórek, kodowania instrukcji ani kolejności bajtów.
- `StubTarget` drukuje moduł przez `StubSelector`: każda operacja IR to samodzielny ciąg instrukcji pamięć-pamięć
  (A jako akumulator, `X=0` dla trybu `,X`); dostęp przez wskaźnik i wołanie pośrednie to kod samomodyfikujący, który
  zostaje wyłącznie w selektorze stuba. Komórka 2-bajtowa ma młodszy bajt pod `sym`, starszy pod `sym+1` (cele wybierają
  własne rozmieszczenie); tymczasowe nazywają się `funkcja__t@N`, napisy `funkcja__s@N`, pomocnicze stuba `__x@N`/`__p@N`/`__a@N`
  (znak `@` jest dozwolony w etykietach asemblera, a niedozwolony w identyfikatorach C, więc nazwy się nie zderzają).
  `Peephole` jest prywatnym przebiegiem tego celu. Ramki (zapisywanie komórek), kontrola stosu i wykrywanie rekurencji są
  w front-endzie, bo nie zależą od CPU; sposób zapisu ramki należy do celu.
- Nowy generator produkuje o 35–60% mniej kodu niż dawny (zmienne i stałe są operandami bezpośrednimi, bez kopii do
  tymczasowych): np. `13_strings` 9519 → 4978 B, `10_struct` 2189 → 913 B.
- Bramka `IrGateTests` porównuje asembler wygenerowany dla korpusu (samples, moduły biblioteki, przykłady z `minic.md`;
  4 warianty: tryb obiektowy × optymalizacja) z zapisanym wzorcem `tests/CathodeRay.Tests/ir-gate.txt` bajt w bajt.
  Wzorzec zapisuje wyjście stuba; zmiana generatora, która świadomie zmienia wyjście (jak przejście na IR), regeneruje go: `UPDATE_IR_GATE=1 dotnet test --filter IrGateTests`.

## Interpreter IR, przebiegi i moduły wykonawcze (plan 30, kroki 5–6)

- `IrInterpreter` wykonuje `Ir.Module` bez procesora (pamięć 64 KB, ramki wg `Saved`, wołania pośrednie, inicjalizatory,
  wbudowana konsola `putchar/puthex/putdec`). `IrOracle` w testach porównuje wynik `main` z interpretera z wynikiem
  skompilowanego programu na stubie dla każdego testu używającego `CCodegenTests.RunC` oraz dla samples — semantyka
  front-endu jest więc sprawdzana niezależnie od celu.
- Ramki (zapis komórek w prologu i epilogu) mają tylko funkcje na cyklu grafu wołań: wołanie pośrednie ma krawędzie do
  funkcji o wziętym adresie, wołanie nieznanej funkcji zewnętrznej do funkcji eksportowanych i o wziętym adresie
  (kod zewnętrzny może wołać z powrotem); biblioteka standardowa i konsola nie wołają kodu użytkownika. Pozostałe funkcje
  nie zapisują nic, a lokalne tablice i struktury zapisują się tylko w funkcjach z ramką (limit 64 B).
- Przebiegi IR: mnożenie, dzielenie i reszta bez znaku przez potęgę dwójki to przesunięcie / maska, mnożenie i dzielenie
  przez 1 to kopia; `t = op …; v = t` staje się `v = op …`, gdy `t` jest martwa (tymczasowe nie żyją między instrukcjami C).
- Procedury mnożenia i dzielenia stuba to moduły biblioteki (`stdlib/stub/rt_mul8.s`, `rt_div8.s`, `rt16.s`), linkowane
  przez `cc` na żądanie (nierozwiązane `cc_mul8`, `cc_divmod`, `cc_mul16`, `cc_div16`, `cc_sdiv16`; komórki argumentów
  `cc_w_*` eksportuje `rt16.s`). Gdy moduł jest całym programem (bez linkera), selektor dołącza użyte moduły do wyjścia.

## Linker, runner i interpreter 6502 (plan 30, kroki 7–9)

- Linker: kolejność bajtów `Abs16` wynika z CPU obiektu (6800 big-endian); relokacje `Lo8`/`Hi8` obsługują `#<sym` i `#>sym`.
- Testy: `TargetHarness` składa `Crt0 + Emit`, asembluje z początkami segmentów z `Layout`, ładuje przez `ICpuRunner`
  (`Runners`: stub, 6502) i czyta wynik `main` z `cc_ret`/`cc_ret_h`. `IrConformanceTests` liczy każdą operację IR
  (szerokość × wartości brzegowe, operandy Imm i Cell, wszystkie warunki `BrCmp`) na interpreterze IR i na każdym celu
  z `Runners` — wyniki muszą być bajt w bajt równe.
- `Mos6502` (tests/) to minimalny interpreter NMOS 6502 z tablicami z `mcp_6502_instructions.json`; opcody
  nieudokumentowane, BRK i tryb dziesiętny rzucają wyjątek; stop na KIL lub skoku do samego siebie.

## Cele akumulatorowe generowane z prymitywów (plan 30, krok 10)

- `ByteSelector` składa kod dowolnego CPU z prymitywów `ByteIsa` (A ← bajt, A ← A op bajt, bajt ← A, skoki, stos, wskaźnik);
  cel to ~150 linii składni i instrukcji CPU (`Mos6502Isa`), reszta jest wspólna. Komórki leżą w pamięci absolutnej,
  argumenty w `cc_argN`/`cc_argN_h`, wynik w `cc_ret`/`cc_ret_h`, wołanie pośrednie przez `cc_fp` + `__icall`.
- `Legalizer` (IR → IR) zamienia mnożenie, dzielenie, reszty, przesunięcia o zmienną liczbę, `Sar` oraz duże bloki na wołania
  funkcji z `stdlib/portable/rt.c` (mini-C kompilowane tym samym frontendem; dołączane do modułu jako funkcje lokalne);
  małe bloki (≤ 6 B) rozwija w Load/Store. Porównania ze znakiem odwracają najstarszy bit (xor 128) i używają ciągu SUB.
- Konsola: `stdlib/portable/io.c` (`__io_buf`, `__io_cur`, `putchar`/`puthex`/`putdec`), linkowana jako `RuntimeModules`.
- 6502/65c02: wskaźnik dostępu pośredniego `__p` na stronie zerowej (segment `ZP`, obszar $00E0..$00FF), `LDY #n; LDA (__p),Y`;
  skoki warunkowe jako krótki skok odwrócony + `JMP` (bez ograniczenia zasięgu). Komórki C nie trafiają na stronę zerową
  (świadome uproszczenie: kod większy, ale bez budżetu ZP).
- Testy: `TargetMatrixTests` (samples 01–16 na każdym celu z runnerem = wynik i konsola stuba), `IrConformanceTests`.
- Z80 (krok 11): ten sam `ByteSelector`; `Z80Isa` używa HL jako rejestru adresowego (`LD HL,adres; ADD A,(HL)`, wskaźniki
  `LD HL,(komórka)`; `INC HL` między bajtami), wołanie pośrednie `LD HL,(komórka); CALL __callhl` (`JP (HL)` w crt0), stos od $1000 w dół.
  Odstępstwo od pierwotnego planu: konwencja argumentów jest wspólna dla wszystkich celów bajtowych (`cc_argN`), nie w HL/DE —
  jeden mechanizm zamiast wielu; interpreter w tests/ (`Z80Cpu`) obsługuje podzbiór Z80 i 8080.
- 8080 (krok 12): `Intel8080Isa` — te same prymitywy z mnemonikami Intel (`LXI H,adres; ADD M`, `LHLD`, `PCHL`, `ADD A`/`RAL` dla
  przesunięć), bez instrukcji Z80; `Z80Cpu(intel8080: true)` odrzuca opkody spoza 8080. Nazwy symboli kolidujące z rejestrami
  lub operatorami asemblera (`low`, `c`, `hl` …) dostają przedrostek `cc_r_` (`ByteIsa.Sym`).
- 6800 (krok 13): `M6800Isa` — big-endian: komórka 2-bajtowa ma starszy bajt pod `sym`, więc `LDX komórka` ładuje wskaźnik, a
  `LDAA n,X` / `STAA n,X` czytają i piszą bajty; wołanie pośrednie `LDX komórka; JSR 0,X`. Dane początkowe generuje frontend
  w kolejności bajtów celu (`ICTarget.ByteOrder` → `Lowering`), `.word` w danych zamienia asembler. Do ISA 6800 dopisano
  brakujące PSHA/PSHB/PULA/PULB. Interpreter `Mc6800Cpu` czyta tablicę opkodów z JSON.

## Rzutowania (plan 30, krok 14)

`Ast.Cast` (parser rozpoznaje `(` typ `)` w `Unary`, także `(R (*)(A))`), `TypeChecker.CastType` (skalary i wskaźniki; struct/void błąd)
i `Lowering.CastValue`: zawężenie do bajtu to `Mov` do komórki W=1, rozszerzenie do słowa to `Mov` do komórki W=2 (zero, bo jedyny typ
8-bitowy jest bez znaku), reszta zmienia tylko typ. Flaga `allowPointerIntegerConversion` usunięta: `printf` rzutuje `(const uchar *)v`.

## `void *`, `size_t`, `offsetof` (plan 30, krok 15)

`Declared("void", n>0)` daje wskaźnik do `void`; `Assignable` przepuszcza `void *` ↔ `T *` (z kontrolą `const`), a dereferencja,
indeksowanie i `+`/`-` na `void *` są błędami typów. `offsetof` to wyrażenie stałe (`Ast.OffsetOf` → `TypeChecker.TryConst`),
`<stddef.h>` definiuje `size_t` i `NULL`. `f(void)` oznacza pustą listę parametrów.

## Struktury przez wartość (plan 30, krok 16)

Argument struktury to wskaźnik na oryginał (`f__p`), a callee kopiuje go w prologu do własnej lokalnej struktury (`CopyBlock`). Wynik
struktury wraca przez wspólny bufor `cc_retbuf` (64 B, definiuje crt0 w segmencie DATA, żeby nie był zerowany razem z BSS):
`return` kopiuje do bufora, wołający tuż po `Call` kopiuje z bufora do tymczasowej `f__agg@N` (zapisywanej w ramce funkcji rekurencyjnych).
Pierwsza wersja z ukrytym parametrem `sret` wskazującym tymczasową wołającego była błędna w rekurencji: wołany odtwarzał z ramki tę samą
statyczną tymczasową, do której właśnie zapisał wynik. Bufor poza ramką nie ma tej wady (między `Ret` a kopią nie ma innych wołań).

## Tablice wielowymiarowe (plan 30, krok 17)

Parser koduje dalsze wymiary w nazwie typu jako sufiks (`int[4]`, `int*[4][5]`; gwiazdki przed wymiarami należą do elementu, gwiazdki
z osobnego pola — do wskaźnika na całość), `Declared` składa z tego tablicę tablic, a `(*p)[4]` i parametr `m[][4]` to wskaźnik do
tablicy. `Index` i `Deref` typu tablicowego rozpadają się na wskaźnik (`RawType` daje nierozpadnięty typ dla `sizeof`); w `Lowering`
rozmiar elementu bierze się z typu bazy (`TypeOf(index.Base).Base`), a odczyt wiersza zwraca jego adres.

## `long` / `ulong` (plan 30, krok 18)

Front-end: `Literal` (stałe z przyrostkami L/U i wartości > 65535 mają typ 32-bitowy i nie są składane w 16-bitowej arytmetyce),
`CType.Promote` (ulong > long > uint > int > uchar), `Lowering.Extend` (jedyne rozszerzenie znakiem: `int` → `long`).
IR ma komórki i stałe W=4; `IrInterpreter` liczy na `long`. Wspólny przebieg `WideLegalizer` (IR → IR, przed selektorem, dla stuba
i celów bajtowych) rozbija W=4 na dwie połówki 16-bitowe `sym` i `sym+2` (w BE odwrotnie): dodawanie i odejmowanie z przeniesieniem
liczonym z porównania połówek, przesunięcia o stałą przez `Shl`/`Shr`/`Sar` połówek, porównania przez starszą połowę
(ze znakiem) i młodszą (bez znaku), `Load`/`Store` dwoma dostępami 16-bitowymi. Mnożenie, dzielenie, reszty i przesunięcia o
zmienną liczbę zamienia wcześniej `Legalizer(wide: true)` na wołania `__cc_mul32`, `__cc_divu32`, `__cc_divs32` itd. z `rt.c`
(mini-C na `ulong`, dołączane do modułu jak wersje 16-bitowe). Argument 32-bitowy to dwa argumenty (młodszy, starszy), wynik wraca
młodszą połową w `cc_ret` i starszą w `cc_rethi` (crt0, DATA). Kolejność faz w celach bajtowych: `Legalizer(wide)` → `WideLegalizer` →
`Legalizer(narrow)` → `ByteSelector`; stub pomija drugi `Legalizer`, bo ma własne procedury 16-bitowe.
Wcześniej złapany błąd: `Load` z komórką wskaźnika w tej samej komórce co wynik (`t = *t`) po rozbiciu niszczył adres drugiej połowy —
`WideLegalizer` kopiuje wtedy wskaźnik do pomocniczej.

## Nazwy różniące się wielkością liter (plan 31, krok 1)

Asemblery i linker nie rozróżniają wielkości liter w symbolach, a C tak. Przebieg `CaseFold` (IR → IR, pierwszy w `Emit` każdego celu) zamienia
nazwę z wielką literą na małe litery plus maskę pozycji wielkich w hex (`Foo` → `foo__c1`, `FOO` → `foo__c7`), więc `foo`, `Foo` i `FOO`
(funkcje, globale, lokalne, etykiety) zostają różne, również między osobno linkowanymi modułami.

## Strona zerowa i krótkie skoki 6502/6800 (plan 31, kroki 2, 3, 5)

- `ZeroPageAllocator` (6502/65C02, po legalizacji) przenosi do segmentu `ZP` najczęściej używane nieeksportowane komórki BSS (waga = liczba
  odwołań × 8^głębokość pętli, wskaźniki z premią), do 16 B na moduł; obszar `C_ZP` to $0010–$00FF. crt0 trzyma tam `__p`, `__q`, `cc_arg1..3`,
  `cc_ret`, `cc_t0/1` i zeruje cały ZP (`__zp_start`..`__zp_end`), bo komórki lokalne `static` startują od zera. Operandy ZP mają w asemblerze
  przedrostek `z:` (ca65), co daje relokację Abs8; wskaźnik leżący w ZP idzie wprost do `LDA (zp),Y` bez kopii do `__p` (chyba że wynik odczytu
  trafia do tej samej komórki — wtedy `mustCopy`).
- `BranchRelaxer` po wyemitowaniu funkcji zamienia trójkę `bXX pomiń; jmp cel; pomiń:` na jeden krótki skok, gdy cel jest w zasięgu
  (liczone na układzie z długimi skokami, więc bezpiecznie); rozmiary instrukcji podaje ISA (`Size`).

## Przebiegi IR i moduły rt (plan 31, kroki 6, 7)

- `IrPasses`: po `ForwardTemporaries` działa propagacja stałych, adresów i kopii w bloku podstawowym oraz składanie działań na stałych (także
  `AddrOf + stała` i skoków o znanym wyniku), potem usuwanie zapisów do komórek lokalnych, których nikt nie czyta. Dotyczy tylko komórek
  funkcji (`nazwa__`), których adres nie jest brany. Przy okazji naprawiony błąd: `WideLegalizer` pomijał rozszerzenie `Mov` W=4 ← W=2 w tej samej komórce.
- Procedury `__cc_*` (mnożenie, dzielenie, przesunięcia, bloki, wersje 32-bitowe) to osobne moduły `stdlib/portable/rt_*.c` linkowane raz na żądanie;
  moduł obiektowy tylko deklaruje `.extern`, a dołączanie kopii zostało wyłącznie w trybie całego programu bez linkera.

## Inlining, mnożenie, indeksowanie i rt w asemblerze (plan 31, kroki 8–10)

- `IrInliner` (po `Lowering`, więc wyrocznia widzi to samo): funkcje liści bez ramki i bez wziętego adresu są wstawiane w miejsca wołań, gdy mają
  do 10 instrukcji IR albo są `static` z jednym miejscem wołania (do 40); parametry i wynik przechodzą przez komórki wołanej funkcji, potem
  `IrPasses` składa stałe. Nieużywane funkcje `static` znikają. Eksportowana definicja zostaje.
- `Legalizer`: mnożenie przez stałą z najwyżej trzema bitami albo 2^n − 1 to przesunięcia i dodawania; `uchar` × `uchar` i dzielenie bajtów wołają
  `__cc_mul8`/`__cc_divu8`/`__cc_modu8`. Dla 6502 i Z80 mnożenie 16-bitowe i dzielenie bez znaku są ręcznie w asemblerze (`stdlib/target/…`),
  stoją w `RuntimeModules` przed wersjami z C (`rt_div.c` podzielone na `rt_div.c` i `rt_divs.c`, żeby asembler nie dublował symboli).
- `IndexFusion` + `Ir.LoadIdx`/`StoreIdx` (tylko dla celów z adresowaniem indeksowanym, dziś 6502/65C02): `[s = i << k;] t = &tab + s;` z odczytem/zapisem przez `t`
  na tablicy o znanym adresie do 256 B staje się `lda tab,x` (indeks poza tablicą to zachowanie niezdefiniowane).
- Błąd `Z80Cpu` w testach: `JR` bezwarunkowy liczył cel od adresu przed bajtem przesunięcia; wyszło dopiero na ręcznym dzieleniu Z80.

## Pola bitowe (plan 31, krok 15)

`type name : N;` w `struct`/`union`, typ `char`/`uchar`/`schar`/`int`/`uint`. Pola upakowane od najmłodszego bitu
w jednostce o rozmiarze typu (bez przekraczania jednostki; zmiana rozmiaru typu otwiera nową jednostkę).
Odczyt: załaduj jednostkę, przesuń i zamaskuj (typy ze znakiem: `shl` + `sar` na 16 bitach). Zapis (`=`, `op=`, `++`):
odczyt–modyfikacja–zapis jednostki. Inicjalizator `{...}` wymaga stałych (sklejanych w jeden zapis jednostki).
Niedozwolone: `&pole`, pola anonimowe (`: 3;`), szerokość większa niż typ.

## float (plan 31, krok 17)

`float` i `double` to ten sam typ: 32 bity IEEE-754 pojedynczej precyzji, w IR zwykła komórka szerokości 4 (jak `long`),
więc selektory i `WideLegalizer` niczego o nim nie wiedzą. Działania to wołania procedur z `stdlib/portable/rt_float.c`
(linkowanych raz, na żądanie): `__cc_fadd/fsub/fmul/fdiv`, porównania `__cc_flt/fle/feq/fnz` (`>` i `>=` zamieniają argumenty),
konwersje `__cc_itof/utof/ltof/ultof` i `__cc_ftol/ftoul`. Negacja to XOR bitu znaku. Literały: `1.5`, `2e3`, `0.5f`
(bity liczone w kompilatorze), stałe całkowite konwertowane w czasie kompilacji. Konwersje niejawne przy przypisaniu,
argumentach, `return`, `?:`; całkowite z `float` daje ostrzeżenie. Działania `% & | ^ << >> ~` na `float` są błędem.
Uproszczenia: wynik obcinany (bez zaokrąglania), liczby zdenormalizowane to zero, brak obsługi NaN/Inf w działaniach.
Tekst: `ftoa(float, uchar *buf)` z `<stdlib.h>` (6 cyfr ułamka); `printf` celowo nie ma `%f` — wciągnęłoby do każdego programu
ok. 3 KB arytmetyki 32-bitowej.

## long long (plan 31, krok 16)

`long long` i `unsigned long long` (64 bity). Komórki 8-bajtowe istnieją tylko tuż po obniżeniu funkcji: `Wide8Legalizer` rozbija je
na połówki 32-bitowe (`sym`, `sym+4`; w BE odwrotnie), zanim ruszą przebiegi IR, więc reszta potoku (interpreter, `WideLegalizer`,
selektory) nie zna szerokości 8. Dodawanie i odejmowanie: przeniesienie z porównania połówek; przesunięcia o stałą składane
z przesunięć połówek; porównania decyduje starsza połówka, przy równych młodsza bez znaku. Mnożenie, dzielenie, modulo i
przesunięcia o zmienną liczbę pozycji to wołania `__cc_mul64/divu64/modu64/divs64/mods64/shl64/shr64/sar64` z `rt_ll64.c` z adresami
obiektów (`Materialize` kopiuje stałe do komórek `__w8x*`). Argument `long long` jedzie jak struktura: przez adres kopii
(1 slot), wynik wraca przez `cc_retbuf`. Literały: `123LL`, `5ULL` oraz każda liczba powyżej 32 bitów (`Ir.Imm.High` to starsza połowa).
Konwersja `float` <-> `long long` nie jest obsługiwana (błąd typów). Tekst: `lltoa`/`ulltoa` z `<stdlib.h>`; `printf` nie ma
`%lld` (koszt dzielenia 64-bitowego w każdym programie).

## Plan 32: efekt (kroki 1-13)

Pomiar `tools/compare.py`: mini-C / cc65 1,74 → 1,73, mini-C / SDCC 3,56 → 3,52. Realny zysk dały tylko (krok 5, węższe ramki rekurencji, wycofany: skan liniowy ignorował skoki w przód i dawał zły wynik, zob. `RecursionFrameTests`): usuwanie kodu nieosiągalnego w IR
(−1152 B w 80 wierszach `target-sizes`), `inc` zamiast `adc #0` przy dodawaniu stałej do
`uint` na 6502 (−2..5 B). Reguły peephole (`ldy`, martwy kod po `jmp`, powtórzone `lda #0`) i przebieg `Mov`→`Mov` w `ForwardTemporaries`
działają w testach jednostkowych, ale nie mają efektu na benchach. Krok 9 (łańcuchy `jmp`) wstrzymany: +3 B na `stub`.

Wniosek z `docs/hotspots.md`: `sta ZP ; lda ZP` (122×) i `lda ZP ; sta ZP` (91×) to zwykły przepływ danych między różnymi komórkami, a nie
zbędne kopie. Prawdziwe straty to (a) kopiowanie zmiennej do tymczasowej tuż przed przesunięciem lub działaniem, (b) powtarzane podwyrażenia
(`n - 1` dwa razy w pętli), (c) 16-bitowe porównanie ze znakiem (`eor #128` na obu połówkach, ok. 8 instrukcji). Dalsze zyski wymagają zmian
w Lowering/IR (wybór celu działania bez kopii, CSE w bloku, porównanie ze znakiem przez odjęcie), czyli zadań większych niż S.

## Plan 35: ramki parami i wynik w HL (Z80/8080)

- Prolog/epilog ramki rekurencyjnej: skalar 2-bajtowy z `Function.Saved` odkładany jednym `ld hl,(n); push hl`
  (8080: `lhld n; push h`), odtwarzany `pop hl; ld (n),hl`; komórki 1- i 4-bajtowe oraz agregaty dalej bajtami przez A.
- **ABI wyniku na Z80/8080:** wynik szerokości 2 (`int`, `uint`, wskaźnik, młodsza połowa `long`/`float` po `WideLegalizer`)
  wraca w **HL**, szerokości 1 w **L** (H nieokreślone). `cc_ret`/`cc_ret_h` nie są już kanałem wyniku; starsza połowa
  wyniku 32-bitowego nadal leży w `cc_rethi`, `long long` i struktury w `cc_retbuf` (bez zmian). HL nie jest parą komórek
  (`CellPairs` = `bc`, `de`) ani nie trafia do `SavedAround`; epilog ramki funkcji z wynikiem odtwarza słowa przez DE
  (`pop de; ld (n),de`, 8080: `xchg; pop h; shld n; xchg`), żeby nie zniszczyć HL. Wynik w DE przechodzi do/z HL przez
  `ex de,hl`/`xchg`. crt0 po `call main` zapisuje HL do `cc_ret` (`ld (cc_ret),hl` / `shld cc_ret`), więc testy i narzędzia
  czytają wynik `main` z pamięci jak dotąd.
- **Rutyny asemblerowe i moduły `.s`:** decyzja — zwracają w HL jak funkcje C (jedna konwencja, wołający nie rozróżnia
  callee). `stdlib/target/z80/rt_mul.s` i `rt_div.s` (`__cc_mul`, `__cc_divu`, `__cc_modu`, wołane z `Legalizer`) zostawiają
  wynik w HL i nie piszą `cc_ret`; na 8080 te rutyny są z `stdlib/portable/*.c`, więc dostają ABI z kompilatora. Własny moduł
  `.s` podany do `cc --cpu z80|8080` musi zwracać `int`/wskaźnik w HL, `char` w L (zapis do `cc_ret` jest ignorowany).
  Testy: `RuntimeRoutinesTests.Z80_Assembly_Routines_Return_In_Hl`, `RuntimeRoutinesTests.Assembly_Module_Returns_Int_In_Hl`.
- Stub, 6502/65c02 i 6800 bez zmian (`ByteIsa.ReturnsInResultReg` = false: wynik w `cc_ret`).

## Plan 36: efekt (Z80/8080, wywołanie ogonowe + tidy)

Bajty mini-C Z80 przed/po (przed = `ea53a28`, po = ten commit; kolumny: bench, przed, po):

| bench | przed | po |
|---|---|---|
| fnptr | 98 | 76 |
| fib | 78 | 76 |
| max3 | 55 | 55 |
| sw | 78 | 78 |
| bubble | 268 | 254 |

Skąd spadki: `apply` w `fnptr` woła ogonowo (`call f + ret` → `jp (hl)`, −2 B na miejscu; reszta z kroków 2–4), `bubble` z peepholi. `fib` bez miejsc ogonowych (wołania karmią `+`) stoi. Nic nie rośnie w żadnej kolumnie (`target-sizes.txt`: maleją tylko wiersze z80/8080).

Wywołanie ogonowe (`ByteSelector`, tylko Z80/8080): `Call` z wynikiem i zaraz `Ret` tej samej komórki (void albo 1–2 B, wynik już w miejscu docelowym) zamienia się w `jp`/`jmp` (pośrednie: `jp (hl)`/`pchl`), gdy ramka pusta (`Saved`) i brak zapisów par wokół wołania. Epilog z `ret` zostaje (inne powroty go używają). Rekurencja wzajemna z ramką nie optymalizuje się (test mutacją warunku).

## Plan 38: CpuModel a zamrożone ABI

Konwencja (argumenty `cc_argN`, wynik `cc_ret`/`HL`) jest opisana jawnie w `CpuModel`
(`ArgRegs` puste = ABI v1, `ResultReg` = `hl` tylko na Z80/8080). Opt 1 (argumenty w rejestrach)
wypełni `ArgRegs` bez rewolucji w selektorze (`ByteIsa.ArgCell` już czyta model); zmiana i tak
wymaga decyzji ABI (dual-ABI albo pilotaż na `nes`/`6510`), bo łamie ręczne `.s` i obiekty.
