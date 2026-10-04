# Nowy przepływ testów: selekcja szybka i jedna pełna suita (status: zamknięty)

- [x] **1.** Zmierzyć czasy klas i wskazać najdroższe przypadki IR — Logger pełnej suity pokazał IrConformance branch po 5:07 i 3:02; TargetMatrix Z80/25_longlong 4:23, VReg NES/25_longlong 4:28, TargetMatrix 6809/25_longlong 2:09. To główne hotspoty.
- [x] **2.** Rozbić długie testy zgodności IR na porcje selekcjonowane po rodzinie i celu — IrProgram.AssertConforms przyjmuje opcjonalny cel; tylko IrConformanceTests czyta CATHODERAY_IR_TARGET. Końcowy runner rozdziela IR i macierze Target/VReg na shardy per cel, 4 procesy równolegle; suma shardów sprawdza wszystkie cele/tryby.
- [x] **3.** Dodać jeden runner tools/test_gate.py dla changed, smoke, fuzz, goldens i full — tools/test_gate.py udostępnia changed, smoke, fuzz, goldens i full; common komenda mierzy czasy faz. Full buduje, uruchamia fuzz, selektor, shardy i pozostałe testy raz.
- [x] **4.** Uzupełnić mapowanie zmienionych plików na testy celowane — Selektor używa testów nazwanych bezpośrednio, mapuje 6809/Z80/6502/6800 na macierze i IR odpowiednich CPU, zmiany wspólnego selektora na wszystkie CPU. Nieznana ścieżka kodu przechodzi do full. Selekcja ma 6 testów stdlib.
- [x] **5.** Zaktualizować dokumentację i zweryfikować nowy przepływ — Zaktualizowano AGENTS.md i docs/technical/testing-and-process.md; usunięto stary selector affected_tests.py. Selektor ma 6 testów stdlib. Pełny nowy flow passed: build -warnaserror, leaf/recursion fuzz 60, 8 shardów macierzy, 9 shardów IR, 3046 pozostałych testów, plan hygiene i git diff --check.

Postęp: 5/5 gotowych.
