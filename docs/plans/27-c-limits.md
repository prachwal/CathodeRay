# Mini-C: ograniczenia i skala (status: otwarty)

Rozmiar: S = <1 h, M = kilka h. Kolejność: A → B → C → D.


## A. Semantyka (najpierw, zmienia wyniki)

- [x] **1.** [M] int ze znakiem: porównania <,<=,>,>= przez xor 0x80 na hi; / % i >> arytmetyczne (wrapper na cc_div16); decyzja: int signed, dodać uint? (sam int signed, bez uint)
- [x] **2.** [S] Typowanie literałów: stała <=255 dopasowuje się do drugiego operandu; dwie stałe = int (naprawia 1<<15, 200+100); folding stałych w parserze/checkerze
- [x] **3.** [S] Diagnostyki: CTypeException/CCodegenException z linią (dziś tylko parser ma pozycję); komunikat file:line jak w cc

## B. Składnia

- [ ] **4.** [M] do…while i switch/case/default (switch na uchar/int: łańcuch porównań, break wspólny z pętlami)
- [ ] **5.** [S] sizeof(typ|zmienna), enum (stałe), char jako alias uchar
- [ ] **6.** [S] Złożone przypisania na wskaźnikach: *p += x, p[i] += x, p += n (desugar jak ++; jedno wyliczenie adresu w temp)
- [ ] **7.** [M] Inicjalizatory tablic {1,2,3} i uchar s[] = "abc" (lokalne i globalne; długość z inicjalizatora)
- [ ] **8.** [M] Globalny inicjalizator wskaźnika/napisu (label w DATA: .word + etykieta _h przez lo/hi operator lub init w crt0)

## C. Runtime i skala

- [ ] **9.** [S] BSS > 256 B: crt0 czyści w pętli 16-bit (dziś limit 256 B i błąd codegenu)
- [ ] **10.** [S] cc_mul8/cc_divmod bez .global (spójnie z 16-bit) + test dwóch modułów z uchar * i /
- [ ] **11.** [S] cc_mul8 przez shift-add (dziś pętla b razy, do 255 iteracji); cc_divmod uchar analogicznie
- [ ] **12.** [S] Ramki: ostrzeżenie/limit głębokości stosu (rekurencja ~256 B) w kompilatorze lub w teście runtime

## D. Porządek

- [ ] **13.** [M] Rozbić Codegen.cs (~1900 linii) na partial: Expressions/Statements/Calls/Wide/Helpers; bez zmian zachowania, testy jako siatka
- [ ] **14.** [S] Testy: matryca int (znak/brak) e2e, sample minic per nowa konstrukcja (samples/minic/), hygiene

Postęp: 3/14 gotowych.
