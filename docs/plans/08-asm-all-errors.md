# Asembler: zbieranie wszystkich błędów (status: w kolejce)

- [ ] **1.** Projekt: kolekcja błędów (fazy, synchronizacja, limit) — Gotowe gdy: spisany kontrakt: błędy zbierane per przebieg (parse/pass1/pass2), synchronizacja na granicy linii, limit liczby (np. 20) z adnotacją o ucięciu, listing częściowy z zaznaczonymi liniami błędów
- [ ] **2.** Rdzeń: kontynuacja Pass po błędzie + częściowy listing — Gotowe gdy: Pass kontynuuje po błędzie linii (PC synchronizowany rozmiarem formy long/1), AssemblerException niesie listę (Lista, nie pierwszy), API Assemble rzuca jak dziś gdy 1 błąd
- [ ] **3.** CLI: format listy błędów, kod wyjścia — Gotowe gdy: CLI wypisuje każdy błąd jako plik:linia: komunikat (sortowane), exit 1, częściowa binarka nie powstaje
- [ ] **4.** Aktualizacja testów oczekujących pierwszego błędu — Gotowe gdy: zinwentaryzowane testy liczące na stop-na-pierwszym (WithMessage/Line) — przepisane na listę albo oznaczone jako 1-błędowe, zero cichych zmian semantyki
- [ ] **5.** Testy wielobłędowe + build/test/hygiene — Gotowe gdy: testy źródła z 2-3 błędami (kolejność, plik:linia każdego, limit ucięcia) + build -warnaserror + test + hygiene OK

Postęp: 0/5 gotowych.
