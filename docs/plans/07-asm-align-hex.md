# Asembler: .align + Intel HEX (mniejszy krok linkera) (status: zamknięty)

- [x] **1.** Decyzja zakresu: pełne segmenty vs .align + Intel HEX — Gotowe gdy: jawna decyzja: pełne segmenty (linker wielomodułowy) albo stop na .align + Intel HEX; decyzja zapisana w docs/assembler-capability-gaps.md
- [x] **2.** Dyrektywa .align (dopełnienie zerami/wartością) — Gotowe gdy: .align N[,wypełnienie] dopełnia PC do wielokrotności N w obu przebiegach identycznie; N<1 / zła liczba operandów / fill poza 0..255 = błąd (dowolne N≥1, jak ca65/z80asm — brak wymogu potęgi 2)
- [x] **3.** Wyjście Intel HEX (opcja CLI, suma kontrolna) — Gotowe gdy: cathode asm --format hex pisze Intel HEX (rekordy danych + EOF, suma kontrolna), binarka domyślna bez zmian, test wektora z obliczonym checksum
- [x] **4.** Testy + build/test/hygiene — Gotowe gdy: testy .align (wyrównanie, wypełnienie, błąd N) + HEX (rekordy, checksum, luki) + build -warnaserror + test + hygiene OK

Postęp: 4/4 gotowych.
