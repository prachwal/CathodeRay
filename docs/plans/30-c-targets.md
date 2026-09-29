# Mini-C: mniejszy kod, rzutowania/void*/long/wielowymiarowe, wielocelowość (6502, 8080, Z80, 6800) (status: otwarty)

Rozmiar: S = <1 h, M = kilka h, L = dzień+. Kolejność: A → B → C → D. A zmniejsza kod dla wszystkich celów; C.1 (interfejs celu) przed każdym nowym CPU; interpretery testowe (C.4) przed 8080/Z80/6800.

## A. Poprawki implementacji (najpierw, bo zmniejszają kod dla wszystkich celów)

- [ ] **1.** [M] Ramki tylko tam, gdzie trzeba: prolog/epilog zapisuje komórki wyłącznie funkcji rekurencyjnych (cykl w grafie wołań) i funkcji, których adres jest brany (cel wołania pośredniego); pozostałe funkcje bez PUSH/POP; wołanie pośrednie dodane do grafu wołań i kontroli stosu
- [ ] **2.** [M] Operandy bezpośrednie i mniej temp: x + stała, porównanie z stałą 16-bit, indeks stałą, zmienna/stała jako prawy operand bez kopii do temp; ponowne użycie temp w gałęziach; CALL+RET -> JMP (ogon)
- [ ] **3.** [S] Redukcja siły: mnożenie/dzielenie/modulo przez potęgę dwójki (shifty, maska; dla int ze znakiem poprawka), mnożenie przez małą stałą (dodawania); wyrażenia stałe z jednej strony
- [ ] **4.** [M] Peephole drugiej generacji: ładowanie po zapisie do innej komórki tej samej wartości, podwójne LDX 0, martwe STA do temp (analiza użycia w oknie bloku podstawowego), CPA po LDA już ustawiającym Z

## B. Nowe cechy języka

- [ ] **5.** [M] Rzutowania (T)x: uchar/int/uint/wskaźniki/wskaźniki do funkcji (parser z rozróżnieniem nawiasu typu, checker, codegen konwersji: zawężenie, rozszerzenie zerem/znakiem); po wprowadzeniu usunąć relaks int<->wskaźnik z biblioteki (printf %s przez (uchar *))
- [ ] **6.** [M] void * i size_t: wskaźnik do void (niejawna konwersja do/z T*, bez dereferencji i arytmetyki), <stddef.h> (size_t = uint, NULL, offsetof), sygnatury memcpy/memset/memcmp na void *
- [ ] **7.** [M] Struktury przez wartość: argument (kopia do ukrytego bufora wołającego, przekazany jako wskaźnik) i wynik (ukryty pierwszy parametr sret); przypisanie wyniku wołania do struktury
- [ ] **8.** [M] Tablice wielowymiarowe int m[3][4]: typ tablicy tablic, indeksowanie m[i][j], sizeof, inicjalizatory {{...}}, przekazywanie jako int (*)[4]
- [ ] **9.** [L] long/ulong 32-bit: arytmetyka (+ - * / % << >> & | ^ ~), porównania, konwersje z/do int, literały z L, printf %ld/%lu/%lx, komórki 4-bajtowe, pomocnicze cc_mul32/cc_div32
- [ ] **10.** [S] union, operator przecinka, konkatenacja napisów "a" "b", sekwencja \xHH, # i ## w makrach, enum z sizeof(struct)

## C. Wielocelowość (kompilator nie jest związany ze stubem)

- [ ] **11.** [M] Interfejs celu: ICodegenTarget (nazwa, tłumaczenie/emisja, crt0, biblioteki asemblerowe, układ pamięci LinkerConfig, konwencja wołań, endianness) + TargetRegistry/factory po nazwie (cc --cpu stub|6502|65c02|8080|z80|6800), StubTarget jako pierwsza implementacja; Crt0, io.s, DefaultConfig i wybór ISA przeniesione do celu; testy parametryzowane po celu
- [ ] **12.** [M] Retargeter asm->asm: klasa AsmTranslator per CPU tłumaczy ~42 instrukcji stuba (flagi, adresowanie ,X, ADD/SUB/CPA d8, PUSH/POP, SHL/SHR, INC/DEC bez zmiany C, BCS/BCC) na instrukcje docelowe, z mapą przesunięć operandów dla kodu samomodyfikującego (label+1) i tłumaczeniem bibliotek .s
- [ ] **13.** [M] Cel 6502/65C02: Mos6502Target (A/X, CLC/SEC+ADC/SBC, PHA/PLA, JSR/RTS, ASL/LSR, INC A tylko na 65C02), crt0, io.s, layout; NMOS bez INC A/DEC A rozwijane przez PHA/CLC... z zachowaniem C
- [ ] **14.** [M] Interpretery testowe (w tests/, bez zależności): minimalny 6502 (tylko używane instrukcje) i harness uruchamiający wszystkie testy mini-C na wybranym celu; ta sama macierz testów dla stub i 6502
- [ ] **15.** [L] Cele 8080 i Z80: odwrócone C przy SUB/CP, Z po ładowaniu przez ORA A, indeksowanie przez HL/IX, PUSH PSW, adresowanie a16 przez LDA/STA, interpretery testowe, tabela rozmiar/cykle
- [ ] **16.** [M] Cel 6800: big-endian w adresach i .word (łatanie operandów w odwrotnej kolejności, RelocKind), interpreter testowy

## D. Zamknięcie

- [ ] **17.** [S] Testy e2e per pozycja, samples/minic/17_casts.c, 18_voidptr.c, 19_matrix.c, 20_long.c, benchmark rozmiar/cykle (przed/po pozycjami z A oraz per cel), docs/minic.md i docs/targets.md, hygiene

Postęp: 0/17 gotowych.
