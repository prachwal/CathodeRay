# Asembler: asemblacja warunkowa (.if/.else/.endif) (status: zamknięty)

- [x] **1.** Semantyka: .if/.elseif/.else/.endif, 0=fałsz, zagnieżdżenia, interakcja z .include — Gotowe gdy: spisana semantyka: .if wyrażenie (.elseif/.elif?, .else, .endif), 0=fałsz, niezerowe=prawda, zagnieżdżenia do limitu stosu, .if po ekspansji includów (działa w includowanych plikach). Etykieta na linii .if dozwolona (wiąże PC, jak w ca65/z80asm)
- [x] **2.** Rdzeń: ewaluacja warunku w obu przebiegach, warunek musi być znany w pass 1 — Gotowe gdy: Expression ma operatory relacyjne =, ==, !=, <>, <, <=, >, >= (poziom 0, wynik 1/0, jak ca65/z80asm; unarne </> bez zmian); warunek .if liczony Evaluate w obu przebiegach, nieznany w pass 1 = błąd must be known (jak .org), gałęzie nieaktywne nie definiują symboli ani nie emitują, decyzja stabilna między przebiegami
- [x] **3.** Błędy: niezbalansowane dyrektywy, puste .else, fazowość rozmiarów — Gotowe gdy: .else bez .if, .endif bez .if, niezamknięty .if na końcu pliku, podwójny .else = AssemblerException z plikiem i linią; test każdego
- [x] **4.** Dialekty: rejestracja nazw per dialekt (ca65/mos/intel/zilog/stub) — Gotowe gdy: .if/.else/.endif w stub/ca65/mos, IF/ELSE/ENDIF w intel/zilog (tabela bez rozróżniania wielkości), brak kolizji z mnemonikami/operatorami słownymi (np. IF vs brak)
- [x] **5.** Listing: pominięte linie widoczne, ale bez bajtów — Gotowe gdy: linie z nieaktywnych gałęzi w listingu bez adresu/bajtów (albo z adnotacją), aktywne bez zmian; test listingu
- [x] **6.** Testy: jednostkowe + golden z ca65/z80asm + build/test/hygiene — Gotowe gdy: testy jednostkowe (prawda/fałsz, elseif, zagnieżdżenia, symbole tylko z aktywnej gałęzi, forward-ref w warunku = błąd) + golden z ca65 (.if/.else) i z80asm (IF/ELSE/ENDIF) bajt w bajt + build -warnaserror + test + hygiene OK

Postęp: 6/6 gotowych.
