# Mini-C: linkowanie C i debug (status: zamknięty)

- [x] **1.** Emisja GLOBAL/EXTERN per symbol C + kolejność modułów — Gotowe gdy: każda funkcja/zmienna globalna C → .global, extern → .extern, main z crt0, kolejność: crt0, lib, user
- [x] **2.** cathode cc: sterownik (C→obiekt→link w jednym wywołaniu) — Gotowe gdy: cathode cc prog.c [-o prog.bin] robi C→asm→obj→link (+-f hex, -l listing)
- [x] **3.** Mapa debug: linia C ↔ adres (listing z adnotacją) — Gotowe gdy: listing z adnotacją linii C (komentarz ;c:linia przy emisji) + cathode cc --map
- [x] **4.** Testy end-to-end (2 moduły C) + build/test/hygiene — Gotowe gdy: 2 moduły C wołające się + main, wynik runtime + build -warnaserror + test + hygiene OK

Postęp: 4/4 gotowych.
