# Asembler: makra (bezparametryczne → z parametrami) (status: w trakcie)

- [ ] **1.** Semantyka makr (definicja, wywołanie, zagnieżdżenia, rekurencja) — Gotowe gdy: spisana semantyka: definicja (.macro NAZWA ... .endmacro / MACRO ... ENDM), wywołanie jak mnemonik, zagnieżdżone definicje zabronione, wywołanie w makrze dozwolone, limit rekursji, etykiety w makrze lokalne per rozwinięcie
- [ ] **2.** Makra bezparametryczne (ekspansja przed pass 1, atrybucja) — Gotowe gdy: definicje zbierane przed pass 1 (nie emitują), wywołanie wstawia linie z atrybucją makro:plik:linia, błędy w rozwinięciu wskazują wywołanie i definicję
- [ ] **3.** Parametry pozycyjne (+ domyślne) i ich podstawianie — Gotowe gdy: parametry pozycyjne + domyślne, podstawianie tokenowe w operandach, zła liczba argumentów = błąd, parametr w wyrażeniu działa
- [ ] **4.** Rejestracja nazw per dialekt (.macro/MACRO) — Gotowe gdy: .macro/.endmacro w stub/ca65/mos, MACRO/ENDM w intel/zilog, brak kolizji z mnemonikami, wywołanie makra wygrywa z mnemonikiem o tej samej nazwie (jak w oryginałach) albo jawnie przeciwnie — decyzja w semantyce
- [ ] **5.** Błędy: niezamknięte, redefinicja, limit rekursji — Gotowe gdy: niezamknięte makro, .endmacro bez .macro, redefinicja, przekroczona rekursja = AssemblerException z plikiem i linią; test każdego
- [ ] **6.** Testy + golden z referencjami + build/test/hygiene — Gotowe gdy: testy definicji/wywołań/parametrów/rekursji + golden z ca65 (.macro) i z80asm (MACRO) bajt w bajt + build -warnaserror + test + hygiene OK

Postęp: 0/6 gotowych.
