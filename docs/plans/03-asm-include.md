# Asembler: dyrektywa .include (wieloplikowość) (status: zamknięty)

- [x] **1.** Loader: przedprzebiegowa ekspansja includów z atrybucją pliku (SourceLine + File, błąd plik:linia) — Gotowe gdy: ekspansja przed pass 1 (osobny loader, nie IDirective), SourceLine niesie plik, AssemblerException pokazuje plik:linia, stare komunikaty line N działają dla 1 pliku
- [x] **2.** Dialekty: rejestracja nazw include per dialekt + operand w cudzysłowie — Gotowe gdy: .include w stub/ca65/mos, INCLUDE w intel/zilog, operand "plik"/\'plik\', wpis w DirectiveTable każdego dialektu
- [x] **3.** Strażnicy: detekcja cykli, limit zagnieżdżenia, brak pliku, ścieżki względne + incdir — Gotowe gdy: cykl A->B->A to błąd z łańcuchem, limit głębokości, brak pliku to błąd z linią, ścieżki względne vs plik nadrzędny + --incdir, wstrzykiwalny reader dla testów
- [x] **4.** Listing + CLI: plik w listingu i błędach, katalog bazowy z pliku źródłowego — Gotowe gdy: listing oznacza linie z includów (plik:linia), CLI liczy bazę z katalogu źródła, błąd CLI pokazuje plik include
- [x] **5.** Testy: ekspansja, cykle, błędy + integracja na plikach tymczasowych + hygiene/build/test — Gotowe gdy: testy ekspansji/cykli/błędów na readerze w pamięci + integracja CLI na plikach tymczasowych, build -warnaserror + test + hygiene OK

Postęp: 5/5 gotowych.
