# Asembler: symbole lokalne + .incbin (status: zamknięty)

- [x] **1.** Semantyka tanich etykiet per dialekt (zakres, kolizje, forward) — Gotowe gdy: wybrana składnia tanich etykiet per dialekt (np. @name w ca65-rodzinie, lokalne $ w z80asm-stylu), zakres = między etykietami globalnymi, kolizja w różnych zakresach dozwolona, forward-ref w zakresie działa
- [x] **2.** Rdzeń: rozwiązywanie lokalnych, unikalność, listing — Gotowe gdy: LineParser/TwoPassAssembler rozwijają lokalne do unikalnych wewnętrznych (stabilne w obu przebiegach), duplikat w tym samym zakresie = błąd, listing pokazuje zapis źródłowy
- [x] **3.** Dyrektywa .incbin (binarka do obrazu, błędy) — Gotowe gdy: .incbin wstawia bajty pliku do obrazu (jak .byte), brak pliku/poza 64KB = AssemblerException z plikiem i linią, ścieżki jak w .include (względne + --incdir)
- [x] **4.** Rejestracja .incbin per dialekt — Gotowe gdy: .incbin w stub/ca65/mos, INCBIN w intel/zilog, operand w cudzysłowie jak .include
- [x] **5.** Testy + build/test/hygiene — Gotowe gdy: testy kolizji lokalnych w dwóch procedurach, pętli z forward-ref, .incbin z binarką + błędy, build -warnaserror + test + hygiene OK

Postęp: 5/5 gotowych.
