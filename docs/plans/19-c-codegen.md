# Mini-C: codegen na stub (status: zamknięty)

- [x] **1.** Emitter wyrażeń (stałe, zmienne, operatory, konwersje) — Gotowe gdy: stałe/zmienne/tablice[i]/wskaźniki, wszystkie operatory przez opcode+mul8/divmod, konwersje uchar/int · v1: pelny uchar; int bez *,/,%,<<,>>, &,|,^ (jawny CCodegenException); Deref/AddressOf/Index jawny blad (plany 20/23); 11 testow C->asm->run.
- [x] **2.** Emitter sterowania (if/while/for, short-circuit &&/||) — Gotowe gdy: if/else (BCC/BCS), while/for (DEX/BNE), &&/|| short-circuit, switch jako łańcuch CPX
- [x] **3.** Emitter funkcji (wołania, tmp, konwencja z docs/stub-calling-conv.md) — Gotowe gdy: wywołania wg docs/stub-calling-conv.md (A=arg1/wynik, X=arg2), tmp na zagnieżdżenia, CALL/RET
- [x] **4.** Alokacja: A/X/komórki, życie zmiennych — Gotowe gdy: A na wartości, X na liczniki/indeksy, komórki na resztę, życie od przypisania do ostatniego użycia
- [x] **5.** Testy: C→asm→run per program + build/test/hygiene — Gotowe gdy: 10+ programów C→asm→run z wynikiem (fib, sort, mul/div) + build -warnaserror + test + hygiene OK

Postęp: 5/5 gotowych.
