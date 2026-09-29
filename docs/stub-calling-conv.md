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

- 1\. argument w `A`, 2\. w `X`; więcej — przez umówione komórki (`arg1`, `arg2`).
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
dopisuje 2 znaki hex. Kolejność linkowania: crt0, lib, program (entry =
pierwsza linia CODE; zła kolejność wykonuje bibliotekę jako program).

## Ekran 40x25 (screen.s → Markdown)

` samples/stub/lib/screen.s`: bufor `__scr_buf` (1000 B, wierszami) + kursor
`__scr_cur`/`_h` (0..999). `scr_putc` (A = znak) dopisuje (po 999 gubi),
`scr_clear` zeruje bufor i kursor. Strony 256 B wybierane skokami
(X ma 8 bitów): osobne etykiety `__scr_buf`, `+256`, `+512`, `+768`.
Dekoder po stronie hosta (`cathode stub run … --screen-at ADDR
[--screen-out plik.md]`): 1000 B → 25 wierszy po 40 znaków (0/niedrukowalne
to spacja, końcowe spacje cięte), plik `.md` to nagłówek + blok text.

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
