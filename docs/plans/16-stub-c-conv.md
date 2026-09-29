# Stub: konwencja wołań i wzorce mini-C (status: zamknięty)

- [x] **1.** Spec konwencji: rejestry, argumenty, wynik, zmienne, stos, calle-saved — Gotowe gdy: spisane w docs/stub-calling-conv.md: A=wartość/arg1/wynik, X=licznik/arg2, zmienne jako absolutne (brak SP+n), caller czyści, calle zachowuje X? decyzja + stos: PUSH/POP tylko A, SP start FF
- [x] **2.** Wzorce codegen: if/while/for, operatory, wywołania zagnieżdżone — Gotowe gdy: wzorce w docs: if (CPA/BEQ), while (DEX/BNE), for, ==/!=/</> (CPA+BCS/BCC), wywołanie f(g(x)) przez komórki tmp, tablice przez ,X, 16-bit przez ADC
- [x] **3.** Demo mini-C: program łamiący wszystkie konwencje + test runtime — Gotowe gdy: samples/stub/features/minic.s używa każdej konwencji (min. 2 poziomy zagnieżdżenia, pętla, warunek <, wywołanie z arg) + test runtime z oczekiwanymi
- [x] **4.** Docs + build/test/hygiene — Gotowe gdy: docs spójne z kodem (adresy z symboli, nie na sztywno) + build -warnaserror + test + hygiene OK

Postęp: 4/4 gotowych.
