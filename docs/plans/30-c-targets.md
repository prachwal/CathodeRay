# Mini-C: kod pośredni (IR) i wielocelowość (6502, Z80, 8080, 6800), rzutowania/void*/long (status: otwarty)

Projekt wg analizy Opusa: szew między front-endem a celami to mały typowany IR (Cell/Imm/AddrOf; Mov, Bin, Un, Load, Store, CopyBlock, BrCmp, Jmp, Label, Call, Ret, Src, Raw), a nie interfejs prymitywów instrukcji stuba. Front-end nie zna flag, ABI, rozmieszczenia komórek, kodowania operandów ani kolejności bajtów; wszystko to należy do klasy celu (ICTarget) wybieranej fabryką po `cc --cpu`. Kod samomodyfikujący zostaje wyłącznie w StubTarget. Bez metod HasXxx w interfejsie: wybór (np. wstawka czy helper dla mnożenia) należy do selektora celu.

Rozmiar: S = <1 h, M = kilka h, L = dzień+. Kolejność: A -> B -> C -> D. Kroki A.2 i A.4 mają bramki (asembler stuba bajt w bajt, potem zestaw testów zachowania). Nie robić: prymitywnego IMachine, optymalizacji na tekście stuba, `long` przed IR, pełnych emulatorów CPU.

## A. Migracja do IR (najpierw; każdy krok zostawia wszystkie testy zielone)

- [x] **1.** [S] Rejestr celów i --cpu: ICTarget (Name, AssemblerCpu, Endianness, StackLimit, DefaultLayout, Crt0, RuntimeModules, Emit) i CTargets.All/Find (ten sam kształt co AssemblerTargets); StubTarget przejmuje crt0, io.s, DefaultConfig z CcCommand i nazwę ISA; inne nazwy dają czytelny błąd 'target not implemented'
- [x] **2.** [M] IR pod spodem: Codegen zamiast AppendLine buduje IrModule (Ins: Raw z tekstem stuba), StubTarget drukuje go bez zmian; bramka: asembler wyjściowy wszystkich samples i testów bajt w bajt taki sam jak przed zmianą (test różnicowy), Peephole staje się prywatnym elementem StubTarget
- [x] **3.** [M] Dane i komórki do IR: IrData z typowanymi wartościami (nie bajtami), Cell z szerokością W (1/2/4) zamiast par lo/hi, znika Hi(); nazewnictwo x_h / x+1 i kolejność bajtów należą do celu
- [x] **4.** [L] Zamiana lowering na prawdziwe instrukcje IR po jednym pliku na commit: Pointers -> Load/Store/CopyBlock, Conditions -> BrCmp (porównanie i skok razem), Calls -> Call/Ret (ABI w celu), Expressions/Wide -> Bin/Un/Mov; metryka: liczba Raw spada do 0; przeniesione ciała emisji trafiają do StubTarget (łatanie operandów zostaje tylko tam)
- [x] **5.** [M] Interpreter IR (~300 linii): wyrocznia front-endu niezależna od CPU, uruchamia cały istniejący zestaw testów C; przebiegi na IR: ramki tylko dla funkcji rekurencyjnych i o wziętym adresie (NeedsFrame), wołanie pośrednie w grafie wołań, redukcja siły, stałe i operandy bezpośrednie, martwe zapisy tymczasowych
- [x] **6.** [S] Helpery mnożenia i dzielenia (Codegen.Helpers.cs) do stdlib/stub/rt.s, linkowane na żądanie przez istniejącą pętlę nierozwiązanych symboli; znikają flagi _needMul*

## B. Linker i infrastruktura testowa

- [x] **7.** [M] Linker: kolejność bajtów Abs16 z CPU obiektu (6800 big-endian), nowe RelocKind.Lo8/Hi8 dla #<sym i #>sym (zamiast ukrytych komórek .word), testy jednostkowe z obiektami 6800
- [x] **8.** [M] Runner niezależny od CPU: crt0 zapisuje wynik main w cc_ret/cc_ret_h przed zatrzymaniem, CcRun.Run(źródło, cpu) z IRunner (load/step/halted) per CPU, macierz testów: cały zestaw na stub i IR, wybrany podzbiór różnicowy na pozostałych celach (wynik i konsola równe stubowi), testy zgodności operacji IR (op x szerokość x wartości brzegowe 0, 1, 0x7FFF, 0x8000, 0xFFFF, granice przeniesienia, porównania ze znakiem)
- [x] **9.** [M] Interpreter 6502 w tests/ (tylko używane opkody, wyjątek na resztę, tablice dekodowania i cykle z JSON ISA) + testy pojedynczych instrukcji z ręcznie policzonymi flagami; spike: ręczny int main(){return 40+2;} przechodzi przez runner

## C. Cele

- [x] **10.** [M] Mos6502Target i 65C02: komórki i wskaźniki na stronie zerowej (budżet + wspólny ZP scratch), Load/Store przez LDY #off; LDA (zp),Y, wołanie pośrednie przez JSR cc_icall (JMP (cc_fp)), CLC/SEC + ADC/SBC, PHA/PLA, INC A tylko na 65C02, crt0, io.s, rt.s, DefaultLayout; dołącza do macierzy
- [x] **11.** [L] Z80Target: konwencja HL=arg1, DE=arg2, wynik HL (reszta w cc_argN), Load przez LD HL,(p); LD A,(HL), Add16 przez ADD HL,DE / SBC HL,DE, pamięć podręczna zawartości HL/A w bloku podstawowym, ramki przez PUSH HL, wołanie przez CALL cc_callhl (JP (HL)), porównania ze znakiem przez odchylenie EOR 128, interpreter Z80 (podzbiór) w tests/
- [x] **12.** [M] Intel8080Target: ten sam selektor ograniczony do podzbioru 8080 z wydrukiem mnemonikami Intel (PCHL, LHLD, DAD), bez IX/IY; interpreter dzieli podzbiór z Z80
- [x] **13.** [M] M6800Target: LDX p; LDAA off,X, wołanie JSR 0,X, konwencja A:B, big-endian w danych i .word, interpreter 6800

## D. Cechy języka i zamknięcie (po IR; front-end, dlatego niezależne od celów)

- [x] **14.** [M] Rzutowania (T)x: uchar/int/uint/wskaźniki/wskaźniki do funkcji (rozróżnienie nawiasu typu w parserze, checker, konwersje w IR: zawężenie, rozszerzenie zerem/znakiem); po wprowadzeniu usunąć relaks int<->wskaźnik z biblioteki (printf %s)
- [x] **15.** [M] void * i size_t: <stddef.h> (size_t = uint, NULL, offsetof), niejawna konwersja void * <-> T *, brak dereferencji i arytmetyki, sygnatury memcpy/memset/memcmp na void *
- [ ] **16.** [M] Struktury przez wartość: argument (kopia wołającego, przekazana jako wskaźnik) i wynik (ukryty parametr sret)
- [ ] **17.** [M] Tablice wielowymiarowe int m[3][4]: typ tablicy tablic, m[i][j], sizeof, inicjalizatory {{...}}, int (*)[4] jako parametr
- [ ] **18.** [L] long/ulong 32-bit jako Cell z W=B4: legalizacja w TargetBase (rozbicie na operacje bajtowe/16-bitowe), rt.s (mul32/div32) per cel, literały L, printf %ld/%lu/%lx
- [ ] **19.** [S] union, operator przecinka, konkatenacja napisów, \xHH, # i ## w makrach, enum z sizeof(struct)
- [ ] **20.** [S] Testy e2e per pozycja, samples/minic/17_casts.c, 18_voidptr.c, 19_matrix.c, 20_long.c, tabela rozmiar/cykle per cel (golden tylko dla rozmiaru, nie dla poprawności), docs/targets.md (jak dodać CPU), docs/minic.md, hygiene

Postęp: 15/20 gotowych.
