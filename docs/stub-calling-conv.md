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
- Caller: nic nie zachowuje (rejestry ulotne). Callee: nie rusza `SP`
  (poza własnymi `PUSH`/`POP` w parach) i odtwarza `X`, jeśli go używa
  niezgodnie z rolą (przez `PUSH`/`TXA` + odtworzenie).
- Brak ramki stosowej: stub nie adresuje względem `SP`, więc zmienne lokalne
  to zwykłe komórki absolutne (jak globalne). Rekurencja wymaga ręcznego
  odkładania stanu na stos.

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
