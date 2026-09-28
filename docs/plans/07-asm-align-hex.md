# Asembler: .align + Intel HEX (mniejszy krok linkera) (status: w trakcie)

- [x] **1.** Decyzja zakresu: pełne segmenty vs .align + Intel HEX — Gotowe gdy: jawna decyzja: pełne segmenty (linker wielomodułowy) albo stop na .align + Intel HEX; decyzja zapisana w docs/assembler-capability-gaps.md
- [ ] **2.** Dyrektywa .align (dopełnienie zerami/wartością) — Gotowe gdy: .align N[,wypełnienie] dopełnia PC do wielokrotności N w obu przebiegach identycznie, N niepotęgą 2 / poza zakresem = błąd
- [ ] **3.** Wyjście Intel HEX (opcja CLI, suma kontrolna) — Gotowe gdy: cathode asm --format hex pisze Intel HEX (rekordy danych + EOF, suma kontrolna), binarka domyślna bez zmian, test wektora z obliczonym checksum
- [ ] **4.** Testy + build/test/hygiene — Gotowe gdy: testy .align (wyrównanie, wypełnienie, błąd N) + HEX (rekordy, checksum, luki) + build -warnaserror + test + hygiene OK

Postęp: 1/4 gotowych.
