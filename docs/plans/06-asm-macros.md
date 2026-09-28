# Asembler: makra (bezparametryczne → z parametrami) (status: zamknięty)

- [x] **1.** Semantyka makr (definicja, wywołanie, zagnieżdżenia, rekurencja) — Gotowe gdy: semantyka spisana i zaimplementowana: .macro nazwa args/.endmacro (ca65/stub/mos), NAZWA: MACRO/ENDM + LOCAL (zilog/intel), podstawianie tokenowe po nazwie, wywołanie jak mnemonik (makro wygrywa), .LOCAL→unikalne __Mn_name per rozwinięcie (bez LOCAL duplikat jak w referencjach), puste parametry dozwolone, za dużo = błąd, rekurencja limit 32, definicje bezwarunkowe (odstępstwo: .if wokół definicji ignorowany, udokumentowane), etykieta na wywołaniu → osobna linia, klamer {} nie ma (przecinki tylko przez nawiasy/cudzysłowy)
- [x] **2.** Makra bezparametryczne (ekspansja przed pass 1, atrybucja) — Gotowe gdy: definicje zbierane przed pass 1 (nie emitują), wywołanie wstawia linie z atrybucją makro:plik:linia, błędy w rozwinięciu wskazują wywołanie i definicję
- [x] **3.** Parametry pozycyjne (+ domyślne) i ich podstawianie — Gotowe gdy: parametry pozycyjne + domyślne, podstawianie tokenowe w operandach, zła liczba argumentów = błąd, parametr w wyrażeniu działa
- [x] **4.** Rejestracja nazw per dialekt (.macro/MACRO) — Gotowe gdy: .macro/.endmacro w stub/ca65/mos, MACRO/ENDM w intel/zilog, brak kolizji z mnemonikami, wywołanie makra wygrywa z mnemonikiem o tej samej nazwie (jak w oryginałach) albo jawnie przeciwnie — decyzja w semantyce
- [x] **5.** Błędy: niezamknięte, redefinicja, limit rekursji — Gotowe gdy: niezamknięte makro, .endmacro bez .macro, redefinicja, przekroczona rekursja = AssemblerException z plikiem i linią; test każdego
- [x] **6.** Testy + golden z referencjami + build/test/hygiene — Gotowe gdy: testy definicji/wywołań/parametrów/rekursji + golden z ca65 (.macro) i z80asm (MACRO) bajt w bajt + build -warnaserror + test + hygiene OK

Postęp: 6/6 gotowych.
