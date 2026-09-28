# Asembler: dyrektywy ca65 long-tail (status: w kolejce)

- [ ] **1.** Inwentaryzacja: które dyrektywy long-tail mają sens (bez linkera: .out/.warning/.error/.assert/.define/.ifblank/.paramcount) — Gotowe gdy: spisane które wchodzą (komunikaty, asercje, .define, warunkowe parametrów) a które nie (linkerowe, .zeropage, smart) + decyzja w gaps
- [ ] **2.** .out/.warning/.error/.assert (komunikaty i asercje w asemblacji) — Gotowe gdy: .out/.warning/.error/.assert działa w obu przebiegach raz (bez dubli), .error przerywa z listą, .assert z komunikatem
- [ ] **3.** .define (makra liniowe C-style, 1-liniowe) — Gotowe gdy: .define NAZWA tekst i .define F(x) tekst, podstawianie tokenowe w linii (nie w cudzysłowach), rekurencja limitowana
- [ ] **4.** .ifblank/.ifnblank/.paramcount (sterowanie parametrami makr) — Gotowe gdy: .ifblank/.ifnblank na resztę linii w makrze, .paramcount jako symbol w makrze (liczba argumentów), działają z pustymi parametrami
- [ ] **5.** Testy + golden z ca65 + build/test/hygiene — Gotowe gdy: testy każdego + golden z ca65 bajt w bajt + build -warnaserror + test + hygiene OK

Postęp: 0/5 gotowych.
