# Mini-C: linkowanie C i debug (status: w kolejce)

- [ ] **1.** Emisja GLOBAL/EXTERN per symbol C + kolejność modułów — Gotowe gdy: każda funkcja/zmienna globalna C → .global, extern → .extern, main z crt0, kolejność: crt0, lib, user
- [ ] **2.** cathode cc: sterownik (C→obiekt→link w jednym wywołaniu) — Gotowe gdy: cathode cc prog.c [-o prog.bin] robi C→asm→obj→link (+-f hex, -l listing)
- [ ] **3.** Mapa debug: linia C ↔ adres (listing z adnotacją) — Gotowe gdy: listing z adnotacją linii C (komentarz ;c:linia przy emisji) + cathode cc --map
- [ ] **4.** Testy end-to-end (2 moduły C) + build/test/hygiene — Gotowe gdy: 2 moduły C wołające się + main, wynik runtime + build -warnaserror + test + hygiene OK

Postęp: 0/4 gotowych.
