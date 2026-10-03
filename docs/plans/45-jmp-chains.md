# Lancuchy jmp: przekierowanie jmp L1 -> jmp L2 z usuwaniem martwych etykiet (GO z planu 44 P4) (status: w kolejce)

- [ ] **1.** [S] Matcher lancuchow w peephole stuba: jmp L1 gdzie L1 zawiera wylacznie jmp L2 (maks. 4 poziomy, wykrywanie cykli). Tylko stub (JMP=3B); 6502/Z80 maja relaksacje.
- [ ] **2.** [S] Warunki bezpieczenstwa: dokladnie 1 poprzednik + brak fall-through do L1. 19 lancuchow z ryzykiem z P4 jako testy negatywne.
- [ ] **3.** [S] Usuwanie martwej etykiety L1 po przekierowaniu (tego zabraklo w planie 32). Test: 07_control.c najpierw bez wzrostu, potem spadek.
- [ ] **4.** [S] Pomiary i zamkniecie: weryfikacja 569 lancuchow z P4 na target-sizes, compare.py, fuzz, hygiene, commit.

Postęp: 0/4 gotowych.
