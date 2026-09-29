# Mini-C: jakość kodu i brakujące typy (float, short, volatile, inline, pola bitowe), strona zerowa 6502, kolizje nazw (status: otwarty)

Cztery luki z porównania z cc65/SDCC (pomiar: 14 małych funkcji, kod mini-C ok. 2,8× większy od cc65 na 6502 i ok. 3,9× od SDCC na Z80): kolizje nazw różniących się wielkością liter (Z80, 8080), zmienne 6502 poza stroną zerową, brak optymalizacji poza peephole stuba i brakujące typy/kwalifikatory (float, short, long long, volatile, inline, pola bitowe).

Rozmiar: S = <1 h, M = kilka h, L = dzień+. Kolejność: A -> B -> C -> D -> E; każdy krok zostawia wszystkie testy zielone (wyrocznia IR + macierz celów + `TargetSizeTests`). Miara postępu części optymalizacyjnej: `docs/compare.md` (rozmiar względem cc65/SDCC) i `docs/target-sizes.md`. Nie robić: alokatora rejestrów ogólnego przeznaczenia przed pomiarem wzorców (5-9), `float` przed `long long` (ta sama maszyneria rozbijania szerokości).

## A. Nazwy

- [x] **1.** [S] Kolizje wielkości liter w asemblerach Intel/Zilog (8080, Z80): ByteIsa.Sym rozróżnia nazwy różniące się tylko wielkością liter deterministycznym sufiksem zależnym od pozycji wielkich liter (spójnym między modułami: funkcje zewnętrzne, globale, extern); test foo/Foo/FOO na z80 i 8080 oraz link dwóch modułów

## B. Strona zerowa 6502

- [x] **2.** [M] Budżet strony zerowej: obszar ZP ($10-$DF), przydział komórek wg wagi użyć z IR (licznik użyć ważony głębokością pętli), operandy zp zamiast absolutnych w Mos6502Isa (krótsze i szybsze), fallback do absolutnych po wyczerpaniu budżetu; symbole ZP między modułami przez relokację Abs8; dotyczy 6502 i 65C02
- [x] **3.** [M] Wskaźniki na stronie zerowej: zmienne i parametry wskaźnikowe alokowane parami w ZP, PtrSetup pomija kopię do __p, gdy komórka wskaźnika już leży w ZP (LDY #n; LDA (zp),Y wprost); tabela rozmiarów przed/po i test, że wyniki się nie zmieniają

## C. Optymalizacje

- [x] **4.** [M] Pamięć podręczna akumulatora w bloku podstawowym ByteSelector: pomijanie LoadA, gdy A już zawiera ten bajt (po StoreA/LoadA; unieważnianie na etykiecie, wołaniu, skoku i zapisie przez wskaźnik); wszystkie cele bajtowe — wraz z INC/DEC pamięci dla x++/x-- (ByteIsa.TryStep) i porównaniem z zerem przez OR bajtów
- [x] **5.** [M] Krótkie skoki warunkowe: dziś skok odwrócony + JMP (6502, 6800); relaksacja po policzeniu adresów (albo dwuprzebiegowo w selektorze) emituje Bcc/JR wprost, gdy cel jest w zasięgu, i skok odwrócony tylko poza nim
- [x] **6.** [M] Przebieg IR: propagacja stałych i kopii w bloku podstawowym, składanie Bin/BrCmp z Imm, martwe komórki lokalne po analizie żywotności na grafie przepływu, lokalne wspólne podwyrażenia; bramka: wyrocznia IR i ir-gate
- [x] **7.** [M] Helpery rt nie jako kopie statyczne w każdym module: wynieść mnożenie/dzielenie/przesunięcia (rt.c) do modułów biblioteki linkowanych raz na żądanie (jak stubowe rt_*.s), w trybie obiektowym zamiast dołączania do modułu (dziś div16 to 1134 B w każdym module)
- [x] **8.** [M] Szybsze mnożenie i dzielenie: uchar*uchar bezpośrednio 8x8, mnożenie przez stałą jako przesunięcia i dodawania, dzielenie przez stałą, wersje asemblerowe mul16/div16 dla 6502 i Z80 (rt_*.s per cel obok wersji przenośnej)
- [x] **9.** [L] Indeksowanie tablic i liczniki pętli w rejestrach indeksowych: wzorzec for (i..) a[i] z tablicą o znanym adresie i 8-bitowym indeksem przez X/Y (6502) lub B/IX (Z80) zamiast wskaźnika w pamięci; rozpoznanie w IR (Load/Store z AddrOf + indeks) i osobna ścieżka selektora
- [x] **10.** [M] Inlining małych funkcji liści na poziomie IR (jeden blok, do N instrukcji, bez rekurencji, bez wziętego adresu): podstawienie parametrów i wyniku, kontrola rozmiaru przed/po (inline tylko gdy nie rośnie)
- [x] **11.** [S] Pomiar: skrypt porównawczy z cc65 i SDCC w tools/ (14 funkcji z docs/compare.md, kompilacja i odczyt rozmiaru CODE) oraz tabela docs/compare.md; cel: średnia geometryczna <= 2x cc65 na 6502 i <= 2,5x SDCC na Z80

## D. Typy i kwalifikatory

- [x] **12.** [S] short, unsigned short, unsigned i signed jako aliasy istniejących typów (short = int, unsigned = uint, unsigned char = uchar); ostrzeżenia bez zmian
- [x] **13.** [M] signed char (schar): 8-bitowy ze znakiem, rozszerzanie znakiem do int/long (Extend), porównania ze znakiem na W1, Sar na W1, warunki BrCmp; char zostaje uchar
- [x] **14.** [M] volatile: każdy odczyt i zapis zachowany (wyłącza propagację, martwe zapisy i pamięć podręczną A dla tej komórki i adresów); inline jako podpowiedź (współpracuje z 10), register przyjmowany i ignorowany
- [x] **15.** [L] Pola bitowe struct { uint a:3; uint b:5; }: układ od najmłodszego bitu w bajcie/słowie, odczyt przez przesunięcie i maskę, zapis jako odczyt-modyfikacja-zapis, sizeof, zakaz adresu pola, inicjalizatory
- [x] **16.** [L] long long / unsigned long long (64 bity): WideLegalizer rekurencyjnie (W8 -> dwie połówki 32), rt mul64/div64 w mini-C, literały LL, konwersje, printf %lld/%llu/%llx, sample
- [x] **17.** [L] float i double (IEEE-754 pojedynczej precyzji, double = float): typ i konwersje z całkowitymi, rt w mini-C na ulong (add, sub, mul, div, porównania, int<->float), literały 1.5f, printf %f z ustaloną liczbą cyfr, brak funkcji matematycznych

## E. Zamknięcie

- [x] **18.** [S] Testy e2e per pozycja, samples/minic/23_*.c ...; docs/minic.md (usunąć spełnione pozycje z „Nie działa”), docs/targets.md, docs/compare.md, docs/target-sizes.md, hygiene

Postęp: 18/18 gotowych.
