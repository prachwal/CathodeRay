# Analiza szkicu „przydział rejestrów dla Z80” (docs/z80-register-optimizer.md)

Stan kodu: gałąź `stub/c-performance`, commit `1ad6396`. Pomiary: listingi `cathode cc <bench>.c --cpu z80 -l` dla 14 benchy z
`samples/bench` (każdy z dopisanym `int main(){return 0;}`, tak jak robi `tools/disasm_compare.py`), rozmiary liczone z adresów
listingu (zgodne z `docs/disasm-z80.md`: max3 154 B, bubble 586 B, fib 212 B, add32 170 B, sum_bytes 90 B). Skrypty pomiarowe
leżały w katalogu tymczasowym sesji (`regsim.py`, `misc.py`); ich logika jest opisana w sekcji (c), żeby dało się ją odtworzyć.

## (a) Werdykt

1. Teza „rejestr jako szybka lokalizacja, `ByteSelector` bez zmian” jest **wykonalna dla wersji 1** (rejestry 8-bitowe, nic nie żyje
   przez `call`). Warunek: mapowanie robi ISA na etapie emisji, tak jak `Mos6502Isa.Mem` (`Mos6502Isa.cs:369-375`), a nie selektor.
   Kryterium „komórka z przedrostkiem `funkcja__`” ze szkicu jest jednak **błędne**. Łapie lokalne `static` (`Lowering.cs:370-378`),
   a gubi komórki z inline'owania i tymczasowe `__lg*`/`__wl*`, wspólne dla całego modułu.
2. Zysk z samych rejestrów jest mniejszy, niż zakłada szkic. Przy K=4 bajtach (B,C,D,E) przydział na całą funkcję, bez współdzielenia
   rejestrów, daje **ok. 21% rozmiaru** (535 B z 2563 B na 14 benchach; max3: 154 → ok. 120 B, a nie 90–100 B). Krok 2 szkicu
   (tylko komórki 1-bajtowe w liściach) daje **34 B, czyli 1,3%**.
3. **Największy zysk leży gdzie indziej** i nie wymaga liveness. Chodzi o aliasowanie parametrów liścia na `cc_argN` (288 B, 11%),
   16-bitowe przesłania przez HL (ok. 290 B), `add hl,de` w łańcuchach 16-bit (ok. 130 B), porównania ze znakiem przez flagę P/V
   (ok. 70 B) i `jr` zamiast `jp` (ok. 49 B). Razem ok. 30% rozmiaru, z tymi samymi testami wyroczni.
4. Kolejność: najpierw naprawić znaleziony błąd poprawności w `Lowering.Frames` i zrobić wspólną analizę liveness, potem tanie
   optymalizacje modelu pamięci i prymitywy 16-bitowe, a rejestry dopiero na końcu (konwencja caller-saved, `push`/`pop` wokół `call`).
   IXH/IXL i argumenty w HL/DE odkładamy.
5. Największe ryzyko to liveness. Istniejąca analiza ramek jest już błędna: rekurencja plus skok w przód daje zły wynik, co
   zweryfikowałem na stubie. `IrInterpreter` tego nie wykryje, bo powtarza tę samą semantykę `Saved` (`IrInterpreter.cs:195-220`).

## (b) Odpowiedzi 1–11

### 1. Wykonalność tezy „ByteSelector bez zmian”

Przejście po ścieżkach (numery linii z `src/CathodeRay.C/ByteSelector.cs`):

| miejsce | co zakłada | co się stanie z rejestrem | minimalna poprawka |
| --- | --- | --- | --- |
| `LoadA`/`StoreA` 82-106 → `Z80Isa.LoadA/StoreA` (`Z80Isa.cs:32-34`) | operand to adres: `ld a,(x)`, `ld (x),a` | `ld a,(c)` jest niepoprawne (błąd asemblera, więc głośny, nie cichy) | `Z80Isa`: `Resolve(text)`; dla rejestru `ld a,c` / `ld c,a` |
| `Alu`/`Cmp` → `Z80Isa.Operate` (`Z80Isa.cs:220-230`) | `ld hl,x` + `op (hl)` | `ld hl,c` się nie złoży | `Operate`: rejestr → `add a,c`, `cp c` (1 B) |
| cache `_acc` (21, 82-106) | klucze to adresy i stałe; każdy zapis idzie przez A | działa bez zmian: `ld c,a` → klucz `max3__m`, a odczyt `ld a,c` jest pomijany jak dziś. Dwie komórki dzielące rejestr mają rozłączne życia, więc stary klucz martwej komórki nie szkodzi. | tylko nowe prymitywy, które piszą bez A (16-bit, `inc bc`), muszą czyścić `_acc`, tak jak `TryStep` (360) |
| `IsVolatile` (71-80), `Key` (67) | tekst adresu, cięcie na `+`/`-` | bez zmian, jeśli selektor dalej widzi nazwy symboliczne (`max3__m+1`), a rejestr pojawia się dopiero w ISA | żadna; komórki `volatile` wykluczamy w alokatorze |
| `TryStep` (355-363) → `Z80Isa.TryStep` (65-94) | `ld hl,x; inc (hl)` | zły kod | ISA: 1 B → `inc c`; para → `inc bc` (kontrakt `ByteIsa.cs:164-170` pozwala nie ustawiać flag) |
| `PtrSetup` (150, 517, 578) → `Z80Isa.PtrSetup` (110-127) | `ld hl,(komórka)`; **przy offsecie > 3 `ld de,off; add hl,de` niszczy DE** (122-123), to samo w 8080 `lxi d; dad d` (`Intel8080Isa.cs:130-131`) | wskaźnik w parze → trzeba `ld l,c; ld h,b`; wartość w D/E cicho ginie | ISA mapuje wskaźnik; alokator nie daje D/E w funkcjach z `Load/Store` o `Off > 3` przez komórkę (albo PtrSetup liczy offset przez A) |
| `CallIndirect` (144) → `Z80Isa.CallIndirect` (102-106) | `ld hl,(komórka)` | jw. | ISA mapuje (`ld l,c; ld h,b`) |
| `ByteOf`/`AddressCell` (175-207) | `AddrOf` → komórka `__aN` w DATA (Z80 nie ma `AddressByte`) | nie dotyczy rejestrów: komórki z `AddrOf` są wykluczone | — |
| `SavedBytes` + prolog/epilog (233-240, 257-265, 270-286) | bajty w pamięci, `push af` | zadziała (`ld a,c; push af`), ale to marnotrawstwo | alokator wyklucza komórki z `Function.Saved` |
| kopie parametrów (242-250) | `ld a,(cc_argN); ld (p),a` | zadziała (`ld c,a`) | później `ld bc,(cc_arg1)` (4 B zamiast 8 B) jako prymityw 16-bit |
| `EmitCall` (671-700), `EmitRet` (702-717) | `cc_arg*`/`cc_ret*` w pamięci | zadziała, bo w v1 rejestr nie żyje przez `call` | — |
| `EmitLoadIdx/StoreIdx` (533-562) | indeks w pamięci | tylko 6502 (`SupportsIndexed`) | — |
| hak 6502 w `EmitBin` (366-383) | `_isa is Mos6502Isa` | nie dotyczy Z80 | — |
| `Header` (719-759) | `.extern` dla `cc_*`, `ExternCells` | komórki rejestrowe nie są `extern` (lokalne, nieeksportowane) | — |
| `PrintBss`/`AppendReserved` (806-843) | drukuje tylko segmenty `ZP` i `BSS` | zostawienie komórki w BSS jest nieszkodliwe (martwe bajty) | w `Tune` ustawić `Segment = "REG"`; `PrintBss` go nie drukuje, więc 0 zmian w selektorze |

Wniosek: v1 wymaga zmian w `Z80Isa` (każdy prymityw plus `PtrSetup`/`CallIndirect`/`TryStep`) i w `Z80Target.Tune`, a w
`ByteSelector` żadnych. Zmiany w selektorze są potrzebne dopiero dla prymitywów 16-bit (pyt. 6) i dla rejestrów żywych przez `call`
(`push`/`pop` wokół `EmitCall`).

**Nazwa rejestru w `Octet.Text` czy pole `Kind`?** Żadne w v1. Lepszy jest wariant trzeci, sprawdzony już na 6502: selektor dalej
operuje nazwami symbolicznymi (`max3__m`, `max3__m+1`), a ISA ma mapę `adres → rejestr` (`AssignRegisters(IReadOnlyDictionary<string,string>)`,
analogicznie do `Mos6502Isa.AddZeroPage`, `Mos6502Isa.cs:44`) i tłumaczy operand w ostatniej chwili. Klucze `_acc`, `IsVolatile`,
`SavedBytes` i komentarze zostają symboliczne, a listing jest czytelny. Tekst rejestru w `Octet` zepsułby dwie rzeczy: `IsVolatile`
i `Key` straciłyby tożsamość komórki, a `Sym()` zamienia `b`/`c` na `cc_r_b` (`ByteIsa.cs:43`, `Z80Isa.cs:11`), więc każda ścieżka
przez `Loc`/`At` musiałaby omijać `Sym`. Pole `Kind` ma sens dopiero wtedy, gdy selektor sam wybiera ścieżkę po klasie operandu
(np. 16-bit „oba bajty w parze”). Wtedy wystarczy zapytanie `bool ByteIsa.IsRegister(string address)`, bez zmiany `Octet`.

**Kryterium kandydata** (zamiast „przedrostek `funkcja__`”):

- `Data` w segmencie `BSS`, nie `Exported`, `Size ≤ 2`;
- symbol występuje w dokładnie jednej funkcji modułu. Wyklucza to kopie z `IrInliner`, bo ciało wstawione do wołającego używa komórek
  wołanego (`IrInliner.cs:3-6`), a funkcja niestatyczna zostaje też osobno. Wyklucza też tymczasowe `__lg{slot}_{w}`
  (`Legalizer.cs:205-214`) i `__wl0`/`__wc0` (`WideLegalizer.cs:129-138`), które są wspólne dla wszystkich funkcji;
- nie jest żywa na wejściu funkcji. To wyklucza lokalne `static` (nazwa `{fn}__{x}` jak zwykła lokalna, `Lowering.cs:375-378`)
  i globalne czytane przed zapisem;
- brak `AddrOf` do symbolu w całym module (także `SymWord` w `Data`), brak w `Module.Volatile`, brak w `Function.Saved`;
- CaseFold zmienia nazwy (`Foo__x` → `foo__x__c1`, `CaseFold.cs:5-7`), więc przedrostek i tak by zawiódł; test „jedna funkcja” jest
  od nazw niezależny.

### 2. Liveness i poprawność

**Algorytm** (konserwatywny, klasyczny, łatwy do uzasadnienia; pracuje na IR po `Legalizer`/`WideLegalizer`, czyli dokładnie na tym,
co widzi `Tune`, `ByteTarget.cs:57-65`, gdzie `CopyBlock`/`Fill` są już rozpisane albo zamienione na `call __cc_copy/__cc_fill`):

1. Bloki bazowe: lider to indeks 0, każda `Label` i instrukcja po `Jmp`/`BrCmp`/`Ret`. Następniki: `Jmp` → cel; `BrCmp` → cel
   i następna; `Ret` → wyjście; reszta → następna. Nieznana etykieta daje „wszystko żywe” (odrzucenie funkcji).
2. `use/def` na jednostkach „symbol komórki” (`Ir.Cell.Sym`, łącznie z połówkami `x+2`). Definicje to `Mov.Dst`, `Bin.Dst`, `Un.Dst`,
   `Load.Dst`, `LoadIdx.Dst` i `Call.Result`. **Definicja zabija tylko wtedy, gdy `Dst.W` równa się rozmiarowi komórki.** Zapis
   węższy (`Cell(x,1)` na 2-bajtowym `x`) liczymy jako użycie plus definicję. Użycia to wszystkie operandy, `Call.Args`,
   `Call.Indirect`, `Ret.Value`, `Load/Store.Ptr`.
3. Iteracja wstecz do punktu stałego (`live_out = ∪ live_in(succ)`). Pętle, `goto` w przód i w tył oraz pętle nieredukowalne obsługuje
   to samo równanie; nie trzeba heurystyki „rozszerz na całą pętlę”.
4. Kandydat odpada, gdy jest w `live_in` wejścia funkcji. W v1 odpada też, gdy jest żywy w `live_out` jakiegokolwiek `Ir.Call`
   (bez `Call.Result`, który jest definicją po wywołaniu).
5. Interferencja: `x` koliduje z `y`, gdy `y ∈ live_out` w punkcie definicji `x`. Kolorowanie zachłanne po wadze z
   `ZeroPageAllocator.LoopWeights` (`ZeroPageAllocator.cs:71-101`): 1 B → dowolny z B,C,D,E; 2 B → para BC albo DE.
   Z80 potrafi też `ld l,c; ld h,e`, więc para jest potrzebna tylko do operacji 16-bit.

**Czy „rejestr nie żyje przez `Call`” wystarcza?** Tak, dla wszystkich wymienionych przypadków, bo po wywołaniu nic nie zakłada
zachowania rejestru:

- rekurencja: w chwili `call` żaden rejestr nie jest żywy, więc druga aktywacja nie może niczego zniszczyć. Parametry funkcji
  rekurencyjnych i tak są zawsze w `Saved` (`Lowering.Frames.cs:159-162`), więc odpadają. Dlatego fib zyskuje tylko 8 B (4%);
- `__callhl` (`Z80Isa.cs:102-106`, `190`) i `__icall`: cel nieznany, ale każdy cel może niszczyć wszystko, a my nic nie trzymamy;
- rt w asemblerze: `rt_mul.s` niszczy BC, DE i HL (`ld bc,(cc_arg2)`, `ex de,hl`), `rt_div.s` tak samo. Jest to bezpieczne,
  bo wywołania rt to zwykłe `Ir.Call` wstawione przez `Legalizer.CallBinary` (`Legalizer.cs:310-320`) przed `Tune`;
- funkcje z `Saved`: komórki z `Saved` wykluczamy. Prolog `Saved` stoi przed kopiowaniem parametrów (`ByteSelector.cs:233-250`)
  i nie dotyka rejestrów;
- `__cc_init`: zwykła funkcja wołana z crt0 przez `__callhl`. Pętla crt0 trzyma HL na stosie i przeładowuje DE (`Z80Isa.cs:171-187`).

Reguła **nie** wystarcza dla dwóch rzeczy spoza wywołań: `PtrSetup` z offsetem > 3 niszczy DE (patrz pyt. 1), a przyszłe prymitywy
16-bit będą używać DE/HL jako scratch. Każdy prymityw ISA musi deklarować zbiór niszczonych rejestrów, a alokator traktuje go
jak mini-`call` dla tych rejestrów.

**Znaleziony błąd (dziś, we wszystkich celach).** `Lowering.Frames` liczy żywe po wywołaniu komórki liniowym skanem z `alreadyWritten`
(`Lowering.Frames.cs:191-219`), który ignoruje skoki w przód. Program:

```c
int g(int n) { int x; x = n * 3; if (n == 0) return 0; g(n - 1);
               if (n & 1) goto skip; x = 100; skip: return x; }
int main() { putdec(g(1)); putdec(g(3)); return 0; }
```

Powinien wypisać `39`. Na stubie (`cathode cc` + `cathode stub run --load 0x1000`, bufor `__io_buf`) wypisuje `0100`, bo `x` nie
trafia do `Saved`. `IrInterpreter` odtwarza tę samą semantykę (snapshot tylko `Saved`, `IrInterpreter.cs:195-220`), a
`TargetMatrixTests` porównuje z stubem, który ma ten sam `Lowering`. Żadna istniejąca wyrocznia tego nie wykryje. Liveness z punktu 2
naprawia oba problemy naraz: `Saved` = komórki w `live_out` któregokolwiek `Call` plus parametry.

**Kontrprzykłady, które muszą być w testach** (każdy na stub/z80/8080/6502/6800 z oczekiwaną wartością wpisaną ręcznie, nie tylko
porównaniem z interpreterem):

1. lokalna `static` licznik w liściu wołanym 3× (wartość musi przetrwać);
2. globalna BSS używana tylko w jednej funkcji i czytana przed zapisem;
3. `x` zdefiniowane przed pętlą, czytane na początku ciała, `call` na końcu ciała (życie przez krawędź wsteczną);
4. `goto` w przód omijające definicję po wywołaniu (program wyżej) oraz `goto` do wnętrza pętli;
5. rekurencja z lokalną żywą przez wywołanie (fib i wariant z `goto`);
6. wołanie przez wskaźnik (`apply(twice, v)`) z wartością żywą przed i po wywołaniu;
7. `a * b` w pętli z licznikiem w rejestrze (rt_mul.s niszczy BC/DE);
8. dostęp do pola struktury z offsetem ≥ 4 przez wskaźnik, gdy w DE żyje inna wartość;
9. zapis częściowy: `(uchar)` na 2-bajtowej komórce, potem odczyt całej;
10. funkcja inline'owana w innej i jednocześnie eksportowana (komórki w dwóch funkcjach);
11. lokalna `volatile`;
12. `long` (połówki `x`, `x+2`) w pętli;
13. dwie komórki o rozłącznym życiu w jednym rejestrze plus `Mov y ← x` (cache `_acc`);
14. `*p` z `p` i wynikiem w tej samej komórce (`mustCopy`, `ByteSelector.cs:517`) przy `p` w parze BC.

### 3. Callee-saved czy caller-saved (otwarte pytanie 1)

Koszty na Z80: `push bc` 1 B/11 T, `pop bc` 1 B/10 T, czyli 2 B/21 T na parę.

| bench | caller-saved (v1 + `push`/`pop` wokół `call`) | callee-saved B,C (i D,E) |
| --- | --- | --- |
| bubble, find, sum_bytes, max3 (liście, bez wywołań) | 0 B / 0 T narzutu | +2 B/+21 T na każdą użytą parę w każdym wywołaniu funkcji (bubble: BC i DE → +4 B, +42 T na wywołanie) |
| fib (n żywe przez 2 wywołania) | `n` w BC: prolog `ld bc,(cc_arg1)` −8 B, 7 odczytów `ld a,c/b` −14 B, `n` znika z `Saved` −16 B, `push`/`pop` wokół 2 wywołań +4 B: **−34 B**; T na aktywację ok. −147 T | `push bc`/`pop bc` raz na aktywację +2 B zamiast `Saved` n −16 B, reszta jak obok: **−36 B**, ok. −168 T |
| rt w asemblerze | bez zmian | `rt_mul.s`, `rt_div.s` muszą zapisywać BC i DE: +4 B każdy, +42 T na wywołanie `*`/`/`/`%` |
| stdlib w C (io.c, rt.c) | bez zmian | kompiluje się tym samym generatorem, więc płaci jak liście |

Na 14 benchach callee-saved kosztuje ok. 2–4 B w każdej z ok. 12 funkcji-liści (+25 do +40 B), a zyskuje 2 B w fib. Żaden bench nie
ma wywołania w pętli, a tylko tam callee-saved wygrywa na czasie. **Rekomendacja: caller-saved.** Krok 5 szkicu realizujemy jako
`push`/`pop` wokół `call` tylko dla komórek żywych przez wywołanie, gdy waga > koszt (2 B + 21 T × waga pętli). Przy okazji rozwiązuje
to rekurencję: komórka w rejestrze zapisana wokół wywołania nie potrzebuje `Saved`. Trzeba poprawić ostrzeżenie o głębokości stosu,
bo `_frames` liczy się w `Lowering` (`Lowering.Frames.cs:238`) i nie widzi tych `push`.

### 4. Argumenty w HL/DE (otwarte pytanie 2)

Zasięg jest mniejszy, niż pisze szkic. Konwencja `cc_arg` żyje tylko w selektorze: `ByteSelector.EmitCall` 671-700, kopie parametrów
242-250, `EmitRet` 702-717 i `ArgSym`/`RetSym` 55-57. `Lowering.Calls` nie zna `cc_arg` (tylko szerokości), `IrInterpreter` przekazuje
argumenty abstrakcyjnie (`IrInterpreter.cs:208-211`), a `Legalizer` tylko wstawia `Ir.Call`. Nie wolno zmieniać konwencji dla:
rt w asemblerze (czyta `cc_arg1`/`cc_arg2` z pamięci), funkcji eksportowanych (inne moduły i stdlib linkowane osobno), celów
`__callhl`/`__icall` (HL niesie adres celu, `Z80Isa.cs:102-106`), `main` i `__cc_init` (crt0).

Minimalny wariant: funkcja `IsStatic`, bez `AddrOf`/`SymWord` do niej, wołana tylko bezpośrednio. Argument 1 (≤ 2 B) idzie w HL,
argument 2 w DE, wynik w HL. `ByteTarget` zaznacza takie funkcje w `Tune`, a selektor w `EmitCall`/prologu wybiera ścieżkę. Takie
funkcje to zwykle właśnie te, których `IrInliner` nie wstawił (większe niż 40 instrukcji albo z kilkoma miejscami wołania).

Oszczędność na benchach: **max3 0 B, add32 0 B, fib 0 B**, bo wszystkie są eksportowane (niestatyczne). Dopiero „podwójne wejście”
(`fib: ld hl,(cc_arg1)` przechodzi w `fib_r:`, wywołania wewnętrzne idą do `fib_r`) daje w fib ok. 18–24 B: 2 miejsca wołania
po 12 → 3 B. Po prymitywach 16-bit argument przez pamięć kosztuje tylko `ld hl,(x); ld (cc_arg1),hl` (6 B), więc zysk z HL/DE
spada do 3–4 B na miejsce wołania. **Rekomendacja: odłożyć** (po planie 33), bo ma niski zwrot i dotyka ABI.

### 5. IXH/IXL (otwarte pytanie 3)

`Z80Cpu` **nie obsługuje prefiksów DD/FD w ogóle**. W `Group3`, `case 5`, gałąź `q` obsługuje tylko `p == 0` (CALL) i `p == 2` (ED),
a DD (`p == 1`) i FD (`p == 3`) rzucają `NotSupportedException` (`Z80Cpu.cs:510-526`). Nie działają więc ani IXH/IXL, ani
`(IX+d)`. Asembler zna je z `data/instructions/mcp_z80_instructions.json` (`LD B,IXH`, `INC IXH`, `(IX+d)`), a `Z80Isa` ma je
w `Reserved` (`Z80Isa.cs:11`). `LD r,r'` jest obsłużone (`Z80Cpu.cs:86-93`); ED 4B/5B/43/53, `sbc hl`, `adc hl` też
(`Z80Cpu.cs:636-680`); `jp po/pe`, `jr cc` i `djnz` również (`Z80Cpu.cs:181-191`, 305-336).

Koszt dodania: obsługa DD/FD jako „HL → IX/IY” w grupach 0–3, plus `(IX+d)` i połówki. To rozmiar M (ok. 150–250 linii plus
testy CPU). Zysk: K=6 (BCDE + IXH/IXL) wobec K=4 to **58 B (2,3%)** na 14 benchach (tabela w c). Operacja na IXH kosztuje 2 B/8 T,
czyli połowę zysku zwykłego rejestru, a z prefiksem DD nie da się mieszać H/L z IXH w jednej instrukcji. **Rekomendacja: nie.**
Jeśli kiedyś DD/FD, to dla IX jako wskaźnika struktur (`ld a,(ix+d)`) w `area`/`move`, nie jako dwóch bajtów rejestrów.

### 6. Operacje 16-bitowe przez HL (krok 6 szkicu)

Prymitywy dostają jawne adresy obu bajtów, nie symbol. Na 6800 `cc_argN` jest młodszy, a `cc_argN_h` starszy (`ByteSelector.cs:55`),
choć komórki są big-endian (`staa max3__a+1` dla `cc_arg1`), więc 16-bitowe `ldx`/`stx` na parze `cc_arg` zamieniłyby bajty. Z80 i 8080
sprawdzają, czy `Hi` to `Lo+1` (albo para `cc_x`/`cc_x_h`, sąsiednie w crt0; `rt_mul.s` już na tym polega), a jeśli nie, zwracają
`false` i selektor wraca do ścieżki bajtowej.

```csharp
/// <summary>Słowo 16-bitowe: stała (liczba albo adres symbolu), albo dwa bajty pamięci/rejestru.</summary>
internal readonly record struct Word(bool IsImmediate, string Lo, string Hi);   // dla stałej: Lo = wyrażenie całego słowa, Hi = ""

// ByteIsa: każda metoda zwraca false = „nie umiem”, selektor używa ścieżki bajtowej (6502/6800 bez zmian).
public virtual bool TryMoveWord(Word dst, Word src) => false;                        // dst ← src
public virtual bool TryAddWord(Word dst, Word a, Word b, bool subtract) => false;    // dst ← a ± b (flagi dowolne)
public virtual bool TryBranchWord(Ir.Cond cond, Word a, Word b, string label) => false;
public virtual bool TryLoadWord(Word dst, int index) => false;                       // dst ← [ptr+index], po PtrSetup
public virtual IReadOnlySet<string> Clobbers => Empty;                               // rejestry niszczone przez prymitywy (DE, HL)
```

Nadpisania i koszty (bez rejestrów, pamięć–pamięć, porównanie z dzisiejszą ścieżką bajtową):

| prymityw | Z80 | 8080 | dziś (Z80) |
| --- | --- | --- | --- |
| `TryMoveWord` | `ld hl,(s)`/`ld hl,nn`; `ld (d),hl`: 6 B, 26–32 T; do/z pary BC: `ld bc,(s)` 4 B, `ld (d),bc` 4 B | `lhld`/`lxi h`; `shld`: 6 B; parami `mov` | 12 B, 52 T |
| `TryAddWord` (add) | `ld hl,(a); ld de,(b)` albo `ld de,nn`; `add hl,de; ld (d),hl`: 10–11 B; stała 1..3 → `inc hl` | `lhld a; xchg; lhld b; dad d; shld d`: 11 B | 20 B |
| `TryAddWord` (sub) | `…; or a; sbc hl,de; …` 13 B | `false` (brak 16-bit sub) | 20 B |
| `TryBranchWord` | `or a; sbc hl,de`: Eq/Ne → `jp z/nz`, bez znaku → `jp c/nc`, ze znakiem → `jp po,$+5; xor 80h; jp m/p` | tylko Eq/Ne przez `mov a,l; ora h` po odjęciu bajtowym; reszta `false` | 20–33 B |
| `TryLoadWord` | `ld a,(hl); inc hl; ld h,(hl); ld l,a; ld (d),hl` 7 B (+PtrSetup) | `mov a,m; inx h; mov h,m; mov l,a; shld d` | 9 B |
| `TryStep` 2 B | bez zmian w pamięci; w parze `inc bc` 1 B | `inx b` | 8 B |

6800 może dostać `TryMoveWord` przez `ldx`/`stx` (6 B zamiast 12 B, tylko gdy obie strony są komórkami big-endian, nie parą `cc_arg`).
6502 zostaje przy domyślnym `false`.

Miejsca wywołania w `ByteSelector`:

- `EmitMov` (339-351): `dst.W == 2` i źródło szerokości 2 (`Cell` W2, `Imm` W2, `AddrOf`), przed pętlą bajtową. `AddrOf` jako
  `ld hl,sym` usuwa też komórki `__aN` (`ByteSelector.cs:196-207`, `fnptr`: `ld a,(__a0)`).
- `EmitBin` → `EmitChain` (411-419): Add/Sub W2 (TryStep zostaje pierwszy, 355-363).
- `EmitBranch` (586-645): W2, każdy warunek, przed rozbiciem bajtowym. Wtedy odpada bias `cc_t0`/`cc_t1` (649-669).
- `EmitLoad` (516-531): `Bytes == 2 && Dst.W == 2`.
- `EmitCall` (673-680) i `EmitRet` (704-711): argument/wynik W2 jako `TryMoveWord(Word(ArgSym(i,0), ArgSym(i,1)), …)`.
- Kopie parametrów w prologu (242-250).
- Po każdym udanym `Try*Word`: `_acc.Clear()` (A mogło zostać, ale zapisy pominęły `StoreA`).

### 7. 8080 i 6800

**Tabela 8080 ze szkicu** (sekcja 5): mnemoniki są poprawne. Są to `mov a,b`, `mov b,a`, `add b` (i `adc`/`sub`/`sbb`/`ana`/`ora`/`xra b`),
`cmp b`, `inr b`/`dcr b`, `lhld`/`shld`, `mov d,b`/`mov e,c`, a zgadza się to z `Intel8080Isa.cs:36-49`, `228-238`. Nieprecyzyjne
jest zdanie „8080 nie ma `ld (nn),r`”: **Z80 też nie ma** `ld (nn),r` dla r ≠ A (ma tylko `ld (nn),a` i 16-bitowe `ld (nn),rp`).
Prawdziwe ograniczenia 8080 wobec Z80:

- brak `ld rp,(nn)`/`ld (nn),rp` dla BC i DE (tylko `lhld`/`shld` plus `xchg`);
- brak `sbc hl`, więc odejmowanie i porównanie 16-bit zostaje bajtowe;
- brak `jr`/`djnz`;
- **brak flagi przepełnienia**: P po `sub` to parzystość, więc porównanie ze znakiem musi zostać przy biasie `xor 128`;
- brak przesunięć CB na rejestrach.

Pułapka testowa: `Z80Cpu` w trybie 8080 ustawia P jako **przepełnienie** dla add/sub (`Z80Cpu.cs:214`, `225`, `SetFlags` 193-201),
a nie parzystość. Kod 8080 używający `jpo`/`jpe` po `sub` przeszedłby testy i nie działałby na prawdziwym 8080. Trzeba to poprawić
w emulatorze przed jakąkolwiek sztuczką na P w 8080 (albo zakazać jej w `Intel8080Isa`).

**Strona bezpośrednia 6800.** Dziś asembler **nie** wybiera trybu bezpośredniego dla komórek kompilatora. `FormSelector` bierze
najkrótszą formę tylko przy znanej wartości, a dla symbolu relokowalnego lub `extern` wybiera najdłuższą (`FormSelector.cs:40-49`).
Listing max3 na 6800 pokazuje wyłącznie 3-bajtowe `ldaa cc_arg1`/`staa max3__a+1`. Relokacja `Abs8` już istnieje
(`TwoPassAssembler.cs:535`, `Linker.cs:224`), ale `M6800Set` nie ma form z przedrostkiem `z:` (`M6800Set.cs:26-33`, porównaj
`Mos6502Set.cs:12-14`). Potrzebne są:

- formy `z:{b}` dla szablonów `d8` w `M6800Set`;
- `M6800Isa.Mem()` na wzór 6502, ale **tylko dla mnemoników z trybem bezpośrednim**. Na 6800 `INC`/`DEC`/`TST`/`CLR`/`NEG`/`COM`/
  przesunięcia pamięci mają tylko `a16` i `d8,X`, co potwierdza `mcp_6800_instructions.json`, więc `TryStep`
  (`M6800Isa.cs:74-99`) musi zostać przy adresie rozszerzonym;
- `M6800Isa.Size` (204-230) musi liczyć 2 B dla `z:`, inaczej `BranchRelaxer` źle policzy zasięgi;
- obszar `C_ZP` w `M6800Target.Layout` i zerowanie w crt0 (`M6800Isa.cs:155-176`);
- `Tune` z `ZeroPageAllocator` bez zmian w algorytmie. Warto też, jak na 6502 (`Mos6502Isa.cs:11-12`), stale umieścić
  `cc_arg1..3`/`cc_ret`/`cc_t*` na stronie bezpośredniej.

**Akumulator B w selektorze z jednym akumulatorem.** Da się go użyć tylko jako jednobajtowej „komórki rejestrowej”: `LoadA` → `tba`,
`StoreA` → `tab` (1 B; nie rusza C, zgodnie z kontraktem `StoreA`), `Alu(Add, first)` → `aba`, `Alu(Sub, first)` → `sba`,
`Cmp` → `cba`, `TryStep` → `incb`/`decb`. **Nie ma** `adc`/`sbc`/`and`/`or`/`eor` A z B, więc `Alu(…, first: false)` i operacje
logiczne na takiej komórce musiałyby spaść na pamięć. W praktyce B nadaje się tylko na licznik 1-bajtowy (sum_bytes `n`, copy),
a pomysł „połówka 16-bitowa w B” wymaga osobnych prymitywów. Niski priorytet.

### 8. Ilościowy szacunek — patrz sekcja (c)

### 9. Wspólny FastStorageAllocator

Wspólna klasa alokatora nie ma sensu. `ZeroPageAllocator` to przydział na moduł bez liveness: komórka zmienia tylko segment
(`ZeroPageAllocator.cs:59`), a wartość przeżywa wywołania, bo to dalej pamięć. Alokator rejestrów działa na funkcję, potrzebuje
liveness, interferencji, klas (bajt/para), reguły wywołań i zbioru niszczonych przez prymitywy. Dwa algorytmy z jedną konfiguracją
każdy to nie abstrakcja. Wspólne mają natomiast analizy: `Operands` jest dziś skopiowane 4 razy (`ZeroPageAllocator.cs:103-116`,
`Legalizer.cs:105-132`, `Lowering.Frames.cs:9-22`, `IrInliner`), a `LoopWeights` raz. Strona bezpośrednia 6800 używa
`ZeroPageAllocator` bez zmian.

```csharp
/// <summary>Fakty o instrukcjach IR wspólne dla przebiegów (jedna definicja operandów, użyć i definicji).</summary>
internal static class IrFacts
{
    public static IEnumerable<Ir.Op> Operands(Ir.Ins ins);
    public static IEnumerable<Ir.Cell> Uses(Ir.Ins ins);
    public static Ir.Cell? Def(Ir.Ins ins);                       // Mov/Bin/Un/Load/LoadIdx.Dst, Call.Result
    public static long[] LoopWeights(IReadOnlyList<Ir.Ins> body);
}

/// <summary>Liveness komórek funkcji (bloki bazowe, punkt stały wstecz).</summary>
internal sealed class IrLiveness
{
    public static IrLiveness Of(Ir.Function function, Func<string, int> cellSize);
    public IReadOnlySet<string> LiveIn(int index);
    public IReadOnlySet<string> LiveOut(int index);
    public IReadOnlySet<string> AtEntry { get; }
}

/// <summary>Rejestry celu dostępne dla komórek.</summary>
internal sealed record RegisterFile(IReadOnlyList<string> Bytes, IReadOnlyList<(string Lo, string Hi)> Pairs);

internal static class RegisterAllocator
{
    /// <returns>Moduł (komórki w rejestrach w segmencie "REG") i mapa adres bajtu (po <c>Sym</c>, np. <c>max3__m+1</c>) → rejestr.</returns>
    public static (Ir.Module Module, IReadOnlyDictionary<string, string> Map) Run(Ir.Module module, RegisterFile registers, Func<Ir.Function, IReadOnlySet<string>> blocked);
}
```

`blocked` zwraca rejestry niedostępne w funkcji (np. D i E, gdy jest `PtrSetup` z offsetem > 3). `Lowering.Frames` przechodzi na
`IrLiveness`.

### 10. Testowanie i bramki

- **Flaga.** `ByteTarget.Emit(module, optimize)` dziś **ignoruje** `optimize` (`ByteTarget.cs:54-66`), choć CLI (`-O0`,
  `CcCommand.cs:250,297`) i `IrProgram.AssertConforms` (`IrProgram.cs:73-77`, pętla `optimize ∈ {true,false}`) je przekazują.
  Wszystkie nowe przebiegi (`Tune` rejestrów, aliasowanie parametrów, prymitywy 16-bit) należy bramkować `optimize`. Wtedy każdy
  istniejący test wyroczni sprawdza obie wersje za darmo, a `-O0` jest punktem odniesienia. Osobne `--no-regs` jest zbędne.
- **IrConformanceTests.** Komórki `c_a`/`c_b`/`c_r` (`IrProgram.cs:17-21`) są globalne. Przy kryterium szkicu (przedrostek) nigdy nie
  trafiłyby do rejestru, więc stwierdzenie „te same testy wyroczni sprawdzają obie wersje” jest nieprawdziwe. Przy kryterium
  z pyt. 1 (jedna funkcja, nieżywe na wejściu) trafią. Trzeba dodać test sprawdzający, że alokator faktycznie coś przydzielił
  (listing zawiera `ld a,c`), inaczej zielone testy nic nie mówią.
- **TargetMatrixTests.** Kontrprzykłady z pyt. 2 (każdy wypisuje wynik przez `putdec`) i program dla błędu `Frames` lepiej trzymać
  jako napisy w osobnych klasach testów niż w `samples/minic`. Każdy plik `??_*.c` dodaje wiersze do tabeli rozmiarów, a
  wyrocznia `TargetMatrixTests` to stub z tym samym błędem `Frames`, więc potrzebna jest wartość oczekiwana wpisana ręcznie
  (`CcRun.RunOn(source, cpu).Console.Should().Be("39")`).
- **Losowe programy.** Generator IR (nie C) na `IrProgram`: pętle (etykieta + `BrCmp` wstecz), `goto` w przód, wywołania funkcji
  pomocniczych (`AddFunction`), rekurencja z ograniczoną głębokością, `Load/Store` przez wskaźnik do tablicy. Porównanie
  `IrInterpreter` ↔ każdy cel, stały seed, ok. 200 programów. Wykrywa błędy liveness rejestrów, bo interpreter nie ma rejestrów.
  **Nie** wykrywa błędów `Saved`/`Frames`, bo interpreter je powtarza. Na to potrzebny jest osobny test z oczekiwaniami ręcznymi
  albo generator z interpreterem o prawdziwych ramkach (kopia `local` na aktywację).
- **TargetSizeTests.** Każde zadanie aktualizuje `target-sizes.txt` przez `UPDATE_TARGET_SIZES=1 dotnet test --filter TargetSizeTests`,
  a w opisie commita podaje diff. Bramka: kolumna zmienianego celu **nie rośnie w żadnym wierszu**, pozostałe kolumny bez zmian.
  Pomocniczo `python3 tools/disasm_compare.py --cpu z80 --all --md docs/disasm-z80.md` przed i po.
- **Pułapki:**
  - `Peephole` działa tylko w `StubSelector` (`StubSelector.cs:34`), nie w celach bajtowych, więc tu nie jest ryzykiem, a jego
    odpowiednikiem jest `_acc`;
  - `BranchRelaxer` (6502/6800, `Mos6502Isa.cs:315`, `M6800Isa.cs:192`) liczy rozmiary z tekstu: każda nowa forma operandu (`z:`,
    `tba`) musi mieć poprawny `Size`, a przy relaksacji `jr` na Z80 trzeba napisać `Size` dla Z80 z rejestrami/ED/DD;
  - `IndexFusion` tylko dla 6502, a indeks `LoadIdx` musi zostać w pamięci;
  - `volatile`: `Module.Volatile` i `Load/Store.Volatile`;
  - przedrostek `cc_r_`: mapa musi być kluczowana nazwami po `Sym()` (jak `AddZeroPage(names.Select(isa.Sym))`, `Mos6502Target.cs:41`),
    bo funkcja `shl` to `cc_r_shl`, a jej lokalne to `shl__…`;
  - CaseFold zmienia nazwy przed `Tune`;
  - połówki `x+2` z `WideLegalizer`;
  - `Ir.Cell` o szerokości innej niż komórka (zapis częściowy).

### 11. Ryzyka, których szkic nie widzi

1. **Błąd `Frames`** (pyt. 2): istnieje już dziś i jest niewidoczny dla obu wyroczni. Rejestry na nim nie polegają, ale wspólna
   liveness powinna go naprawić, zanim ktoś skopiuje algorytm ze skanem liniowym.
2. **Mapa symbol → rejestr jest globalna w module**, a symbole nie są prywatne dla funkcji: tymczasowe `__lg*`/`__wl*`/`__wc*`
   (Legalizer, WideLegalizer) i komórki wołanego po inliningu (IrInliner). Bez reguły „jedna funkcja” ta sama nazwa byłaby rejestrem
   w funkcji A i przez pomyłkę też w funkcji B. Tymczasowe `__lg`/`__wl` mają bardzo krótkie życia, więc to dobrzy kandydaci,
   ale tylko po przemianowaniu per funkcja w `Legalizer.Temp`/`WideLegalizer.Temp` (nazwa `{fn}__lg…`). Zmiana dotyczy tylko BSS,
   bo `TargetSizeTests` mierzy CODE. Na 6502 rośnie jednak konkurencja o 16 B strony zerowej.
3. **Lokalne `static`** mają nazwę jak zwykłe lokalne (`Lowering.cs:375-378`): kryterium szkicu dałoby cichy błąd, bo wartość ginie
   między wywołaniami.
4. **`PtrSetup` niszczy DE** (Z80 i 8080) przy offsecie > 3. To cichy błąd, jeśli DE trzyma komórkę.
5. **Moduły osobne** (rt.c, io.c, stdlib w trybie obiektowym, `ExternCells`): przy caller-saved rejestry **nie mogą** przeciec przez
   granicę modułu. Komórki `extern` nie są w `Data`, więc nie są kandydatami, a każde wywołanie (także `EXTERN`) niszczy wszystko.
   Przeciek jest możliwy tylko przy callee-saved (rt asm i stdlib musiałyby dotrzymać umowy) albo przy argumentach w rejestrach
   dla funkcji nie-statycznych. Obie rzeczy odradzam w planie 33.
6. **Aliasowanie parametrów na `cc_argN` i big-endian**: `Loc(sym,2,0)` na 6800 to `sym+1` (`ByteIsa.cs:37-38`), a `ArgSym(i,0)`
   to `cc_argN`, czyli młodszy bajt pod niższym adresem. Aliasowanie `param → cc_argN` zamieniłoby bajty. Tylko cele LE albo
   mapowanie per bajt.
7. **Równoległe przypisanie w `EmitCall`**: jeśli komórka zaliasowana na `cc_arg1` jest też argumentem 2 wołania, zapis argumentu 1
   zniszczy ją przed odczytem. Aliasowanie tylko w liściach (brak `Call` po legalizacji, łącznie z wywołaniami rt).
8. **Emulator 8080 liczy P jako przepełnienie** (pyt. 7), czyli fałszywa zieleń dla sztuczek z P/V na 8080.
9. **Ostrzeżenie o stosie** (`'g' is recursive: 4 B per cycle`) liczy się przed `Tune` i nie uwzględni `push`/`pop` wokół `call`.
10. **Zysk w pętlach ≠ zysk rozmiaru**: szkic mierzy bajty. Tabela (c) podaje T statycznie (każda instrukcja raz); w bubble wewnętrzna
    pętla wykonuje się n² razy, więc realny zysk czasu jest wielokrotnie wyższy niż w kolumnie T.

## (c) Ilościowy szacunek

**Metoda.** Pomiar: rozmiary funkcji i bajty instrukcji odwołujących się do komórek odczytane z adresów listingu Z80. Szacunek:
wybór K bajtów komórek (lokalne `fn__*` i tymczasowe `__lg/__wl/__wc`) na funkcję, zachłannie po oszczędności bajtów, **na całą
funkcję, bez współdzielenia rejestrów** (odpowiada krokom 2–3 szkicu z pełnymi 2-bajtowymi komórkami). W funkcjach z `call`
komórka kwalifikuje się tylko wtedy, gdy między jej pierwszym a ostatnim użyciem nie ma `call` (benche z `call` nie mają pętli).
Zamiana na rejestr (B,C,D,E):

- `ld a,(x)` / `ld (x),a`: −2 B, −9 T;
- `ld hl,x` + `op (hl)`: −3 B, −13 T;
- `ld hl,x` + `inc/dec (hl)`: −3 B, −17 T.

Dla IXH/IXL (K=6) wartości są o połowę mniejsze (2 B/8 T na instrukcję). Kolumna „+pary16” dodaje `inc bc` zamiast 16-bitowego
`TryStep` (−7 B) i `ld l,c; ld h,b` zamiast `ld hl,(p)` (−1 B, −8 T). T to suma statyczna, nie dynamiczna.

| bench | B funkcji (pomiar) | B instr. z komórkami lokalnymi/tymcz. (pomiar) | B instr. z `cc_*` (pomiar) | K=2 B/T (szac.) | K=4 B/T (szac.) | K=6 = 4 + IXH/IXL (szac.) | K=4 + pary16 (szac.) |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| add32 | 170 | 124 | 30 | 16/72 (9%) | 30/134 (18%) | 37/168 (22%) | 30/134 (18%) |
| bubble | 586 | 438 | 51 | 98/437 (17%) | 152/677 (26%) | 168/757 (29%) | 155/701 (26%) |
| copy | 90 | 63 | 18 | 9/40 (10%) | 15/67 (17%) | 17/77 (19%) | 32/135 (36%) |
| div16 | 135 | 74 | 54 | 10/44 (7%) | 10/44 (7%) | 10/44 (7%) | 10/44 (7%) |
| fib | 212 | 125 | 48 | 8/36 (4%) | 8/36 (4%) | 8/36 (4%) | 8/36 (4%) |
| find | 192 | 118 | 43 | 18/80 (9%) | 34/152 (18%) | 40/182 (21%) | 41/182 (21%) |
| fnptr (4 funkcje) | 302 | 157 | 105 | 36/160 (12%) | 60/268 (20%) | 62/278 (21%) | 60/268 (20%) |
| max3 | 154 | 83 | 50 | 20/90 (13%) | 34/152 (22%) | 40/182 (26%) | 34/152 (22%) |
| mul16 | 76 | 36 | 36 | 8/36 (11%) | 16/72 (21%) | 20/92 (26%) | 16/72 (21%) |
| shift | 64 | 30 | 30 | 8/36 (13%) | 16/72 (25%) | 18/82 (28%) | 16/72 (25%) |
| str_len | 67 | 40 | 12 | 8/36 (12%) | 12/54 (18%) | 13/59 (19%) | 27/122 (40%) |
| structs (2 funkcje) | 273 | 202 | 30 | 51/228 (19%) | 93/419 (34%) | 103/466 (38%) | 93/419 (34%) |
| sum_bytes | 90 | 58 | 15 | 16/72 (18%) | 28/129 (31%) | 30/139 (33%) | 28/129 (31%) |
| sw | 152 | 40 | 21 | 25/112 (16%) | 27/121 (18%) | 27/121 (18%) | 27/121 (18%) |
| **razem** | **2563** | **1588** | **543** | **331 (13%)** | **535 (21%)** | **593 (23%)** | **577 (23%)** |

Krok 2 szkicu (tylko komórki 1-bajtowe, tylko liście, 4 rejestry) daje razem **34 B (1,3%)**, bo prawie wszystkie komórki benchy
to `int`. Współdzielenie rejestrów przez rozłączne przedziały (krok 4) podniesie te liczby w bubble/max3/structs, gdzie
tymczasowe `t@N` żyją krótko, ale nie ponad sumę kolumny „z komórkami lokalnymi” pomnożoną przez ok. 2/3.

**Optymalizacje niezależne od rejestrów** (pomiar = bajty wzorca w listingu; oszczędność = szacunek z tabeli w pyt. 6):

| optymalizacja | bajty wzorca (pomiar, 14 benchy) | oszczędność (szac.) | max3 | add32 | fib | bubble |
| --- | ---: | ---: | ---: | ---: | ---: | ---: |
| aliasowanie parametrów liścia na `cc_argN` (LE) | 288 (tylko liście) | ≈288 B (11%) | −36 | −48 | 0 | −24 |
| przesłanie 16-bit przez HL (`ld hl,(s); ld (d),hl`) | 480 + 108 (epilog `cc_ret` liści) | ≈290 B | −24 | −18 | −36 | −18 |
| łańcuch add/sub 16 przez `add hl,de`/`sbc hl,de` | 340 (17 łańcuchów) | ≈130 B | 0 | −18 | −9 | −56 |
| porównanie ze znakiem przez P/V (Z80) | 104 (bias `cc_t0/cc_t1`) | ≈70 B | −22 | 0 | −3 | −33 |
| `jr` zamiast `jp` (relaksacja jak 6502/6800) | 49 skoków | ≈45 B | −4 | −1 | −2 | −6 |
| `long` Add/Sub bez rozbijania (łańcuch 4 B w `EmitChain`) | 85 (add32) | ≈45 B | 0 | −45 | 0 | 0 |
| **razem** (częściowo się nakładają) | | **≈800–870 B (31–34%)** | **≈154 → 68** | **≈170 → 40–60** | **≈212 → 160** | **≈586 → 450** |

Rejestry po tych krokach dają mniej niż w pierwszej tabeli, bo część `ld a,(x)`/`ld (x),a` znika już wcześniej. Szacunek po
wszystkim (tanie kroki plus rejestry K=4 z liveness): max3 ok. 55–60 B (SDCC 53 B), bubble ok. 350 B (SDCC 276 B), fib ok.
130–150 B (SDCC 31 B; SDCC używa ramki na stosie i IX, czego ten plan nie zmienia).

## (d) Proponowany plan 33

Każde zadanie zostawia zielone `dotnet test` i nierosnącą tabelę rozmiarów. „Tabela” oznacza
`UPDATE_TARGET_SIZES=1 dotnet test --filter TargetSizeTests` z diffem `tests/CathodeRay.Tests/target-sizes.txt` w commicie.
Model: **H** = nadaje się dla Haiku (S, jeden plik, jednoznaczny test), **M** = wymaga mocnego modelu.

1. **Test regresji ramek (błąd `goto` po wywołaniu).** S, H. Plik: nowy `tests/CathodeRay.Tests/FramesLivenessTests.cs`
   (źródło C jako napis, nie w `samples/minic`, bo tam doszłyby wiersze tabeli rozmiarów, a `TargetMatrixTests` porównuje ze stubem,
   który ma ten sam błąd). `[Theory]` po celach z runnerem: `CcRun.RunOn(source, cpu).Console.Should().Be("39")`. Akceptacja:
   `dotnet test --filter FramesLivenessTests` jest czerwony (`0100`). Commit razem z zadaniem 3 (albo z
   `Skip="plan 33 #3"`). Tabela bez zmian.
2. **`IrFacts`: jedna definicja `Operands/Uses/Def/LoopWeights`.** M, M. Pliki: nowy `src/CathodeRay.C/IrFacts.cs`,
   `ZeroPageAllocator.cs`, `Legalizer.cs`, `Lowering.Frames.cs`, `IrInliner.cs`. Akceptacja: `dotnet test` zielony i tabela
   **bajt w bajt bez zmian** (czysty refaktor).
3. **`IrLiveness` i naprawa `Frames`.** M, M. Pliki: nowy `src/CathodeRay.C/IrLiveness.cs`, `Lowering.Frames.cs` (zamiana
   191-220). Akceptacja: test z zadania 1 zielony na wszystkich celach; nowe testy jednostkowe liveness (pętla, `goto` w przód i
   w tył, zapis częściowy) w `tests/CathodeRay.Tests/IrLivenessTests.cs`. Tabela: wiersze z rekurencją mogą się zmienić w obie
   strony (dokładniejszy `Saved`); każdy wzrost uzasadniony w commicie jako naprawa.
4. **Bramkowanie `optimize` w `ByteTarget.Emit`.** S, H. Plik: `src/CathodeRay.C/ByteTarget.cs` (`Tune` tylko gdy `optimize`,
   także ZP 6502). Akceptacja: `dotnet test` zielony; tabela bez zmian (domyślnie `optimize=true`); `cathode cc x.c --cpu 6502 -O0`
   daje kod bez `z:` dla komórek modułu. Test w `TargetSizeTests` (jeden program, `-O0` ≥ domyślny).
5. **Aliasowanie parametrów liści na `cc_argN` (cele LE).** M, M. Pliki: nowy przebieg `src/CathodeRay.C/ParamAlias.cs` wołany
   z `ByteTarget.Emit` przed `Tune`, gdy `optimize && ByteOrder == Little`. Warunki: brak `Ir.Call` po legalizacji, parametr
   niewzięty adresem, nie `volatile`, symbol tylko w tej funkcji; `ByteSelector` pomija kopię, gdy `param.Sym == ArgSym`.
   Akceptacja: kontrprzykład „inline + eksport” i „parametr zmieniany w pętli” w `TargetMatrixTests`;
   `python3 tools/disasm_compare.py --cpu z80 max3` daje ≤ 118 B; tabela: kolumny z80/8080/6502/65c02 nie rosną, 6800 i stub bez
   zmian.
6. **`Word` i `TryMoveWord` w `ByteIsa` + Z80.** M, M. Pliki: `ByteIsa.cs`, `Z80Isa.cs`, `ByteSelector.cs` (`EmitMov`, `EmitCall`,
   `EmitRet`, prolog). Akceptacja: `IrConformanceTests.Moves_Convert_Widths` i `Long_Moves_Loads_And_Stores…` zielone; max3 na Z80
   ≤ 100 B; tabela: z80 maleje w każdym wierszu z przypisaniami 16-bit, inne kolumny bez zmian.
7. **`TryMoveWord` dla 8080 (`lhld`/`shld`).** S, H. Plik: `Intel8080Isa.cs`. Akceptacja: `dotnet test --filter "IrConformance|TargetMatrix"`
   zielony; tabela: kolumna 8080 maleje, pozostałe bez zmian.
8. **`TryAddWord` Z80 (`add hl,de`, `sbc hl,de`, `inc hl` dla stałych 1..3).** M, M. Pliki: `Z80Isa.cs`, `ByteSelector.EmitChain`.
   Akceptacja: `Binary_Operations_Match_The_Oracle(Add|Sub, 2)` zielone; bubble na Z80 ≤ 480 B; tabela z80 nie rośnie.
9. **Porównanie ze znakiem przez P/V na Z80.** S, M (subtelne flagi). Pliki: `Z80Isa.cs` (nowy `bool TrySignedBranch`),
   `ByteSelector.EmitBranch`/`Bytes` (pominięcie biasu, gdy ISA umie). Akceptacja: `Conditional_Branches_Match_The_Oracle` dla
   Lt/Le/Gt/Ge (W1, W2) zielone; max3 na Z80 zmniejsza się o ≥ 18 B; kolumna 8080 **bez zmian** (8080 nie ma V).
10. **Emulator 8080: P = parzystość dla add/sub.** S, H. Plik: `tests/CathodeRay.Tests/Z80Cpu.cs` (`Alu`, 214 i 225: w trybie
    `Intel8080` podawać `Parity`). Akceptacja: nowy test w `Z80CpuTests.cs` (`sub` 8080 ustawia P wg parzystości); cały `dotnet test`
    zielony; tabela bez zmian.
11. **Relaksacja `jp` → `jr` na Z80.** M, M. Pliki: `Z80Isa.cs` (`Relax` przez `BranchRelaxer.Apply`, funkcja `Size` z formami
    ED/CB/rejestrowymi, tylko warunki z/nz/c/nc). Akceptacja: `dotnet test` zielony; sw na Z80 ≤ 135 B; tabela z80 maleje.
12. **`long` Add/Sub/And/Or/Xor/Mov bez rozbijania w celach bajtowych.** M, M. Pliki: `WideLegalizer.cs` (tryb „zostaw
    arytmetykę”, dalej rozbija `Call`/`Ret`/`Load`/`Store`), `ByteTarget.cs`. Akceptacja: wszystkie `Long_*_Match_The_Oracle`
    zielone; add32 na Z80 ≤ 130 B; stub bez zmian (dalej pełne rozbicie).
13. **`Z80Isa`: mapa rejestrów (`AssignRegisters`, `Resolve`) we wszystkich prymitywach.** M, M. Pliki: `Z80Isa.cs` i test
    jednostkowy `tests/CathodeRay.Tests/Z80RegisterIsaTests.cs` (ręczne IR z ręczną mapą `main__x → c`). Akceptacja: listing
    zawiera `ld a,c`/`add a,c`/`inc c`, wynik zgodny z `IrInterpreter`; bez alokatora tabela bez zmian.
14. **Deklaracja niszczonych rejestrów (`Clobbers`) + `PtrSetup` z offsetem > 3.** S, H. Plik: `Z80Isa.cs` (i analogicznie
    `Intel8080Isa.cs` w zadaniu 17). Akceptacja: test w `Z80RegisterIsaTests` (wartość w E przeżywa `Load` z `Off = 6`, bo alokator
    jej tam nie da albo ISA nie używa DE); tabela bez zmian.
15. **`RegisterAllocator` v1 (liveness, jedna funkcja, nieżywe na wejściu, nic przez `call`, pary BC/DE).** L, M. Pliki: nowy
    `src/CathodeRay.C/RegisterAllocator.cs`, `Z80Target.cs` (`Tune`). Akceptacja: `tests/CathodeRay.Tests/RegisterAllocatorTests.cs`
    z 14 kontrprzykładami z pyt. 2 (źródła jako napisy, oczekiwana konsola wpisana ręcznie) zielony na wszystkich celach; `IrConformanceTests` zielone przy `optimize` true/false; test,
    że dla `c_a` w `IrConformance` przydzielono rejestr; sum_bytes na Z80 zmniejsza się o ≥ 20 B; tabela z80 nie rośnie w żadnym
    wierszu.
16. **Losowe programy IR vs `IrInterpreter`.** M, M. Plik: `tests/CathodeRay.Tests/RandomIrTests.cs` (generator na `IrProgram`,
    seed stały, 200 programów, pętle, `goto`, wywołania, wskaźniki). Akceptacja: zielony na wszystkich celach i przy obu
    wartościach `optimize`; czas < 60 s.
17. **Port na 8080 (`mov`, `inr`, `inx`, `xchg`).** S, H. Plik: `Intel8080Isa.cs` + `Intel8080Target.Tune` (2 linie). Akceptacja:
    `dotnet test --filter "IrConformance|TargetMatrix|RandomIr"` zielony; tabela: kolumna 8080 maleje, inne bez zmian.
18. **Komórki żywe przez `call`: `push`/`pop` wokół wywołania.** M, M. Pliki: `RegisterAllocator.cs` (koszt 2 B + 21 T × waga),
    `ByteSelector.EmitCall` (zapis par z listy od ISA), `Lowering.Frames` (komórka w rejestrze nie idzie do `Saved`). Akceptacja:
    kontrprzykłady 5–7 zielone; fib na Z80 ≤ 185 B; tabela z80 nie rośnie.
19. **6800: strona bezpośrednia.** M, M. Pliki: `src/CathodeRay.Assembler/Isa/Targets/M6800Set.cs` (formy `z:{b}`), `M6800Isa.cs`
    (`Mem` tylko dla mnemoników z trybem `d8`, `Size` = 2 dla `z:`, crt0 zeruje ZP, stałe `cc_arg1..3/cc_ret/cc_t*` na ZP),
    `M6800Target.cs` (obszar `C_ZP`, `Tune` z `ZeroPageAllocator`). Akceptacja: test asemblera `ldaa z:x` = 2 B z relokacją
    `Abs8`; `TargetMatrixTests` 6800 zielone; kolumna 6800 maleje w każdym wierszu.
20. **6800: `TryMoveWord` przez `ldx`/`stx`.** S, H. Plik: `M6800Isa.cs` (tylko gdy obie strony to komórki big-endian, nie para
    `cc_arg`). Akceptacja: `Long_Moves…` i `TargetMatrixTests` 6800 zielone; kolumna 6800 maleje.

Poza planem (odłożone z uzasadnieniem w pyt. 3–5): callee-saved, argumenty w HL/DE, IXH/IXL i DD/FD w emulatorze, akumulator B
na 6800.

## (e) Poprawki do docs/z80-register-optimizer.md

1. Sekcja 1, tabela: „argument ALU z komórki `ld hl,nn` + `add a,(hl)` 4 B, **21 T**”: w rzeczywistości 10 + 7 = **17 T**.
2. Sekcja 1: „licznik `x++`: `ld a,(nn)` `inc a` `ld (nn),a` 7 B”: dziś kod używa `TryStep` (`ld hl,nn; inc (hl)`, 4 B, 21 T;
   `Z80Isa.cs:65-94`), więc zysk rejestru to 3 B, nie 6 B.
3. Sekcja 1: „kopia 16 bitów 4 instrukcje, 12 B → `ld d,b` `ld e,c`”: brakuje wariantu bez rejestrów, `ld hl,(s); ld (d),hl`
   (6 B), który daje połowę zysku bez liveness.
4. Sekcja 2: „`ByteSelector` bez zmian”: prawda tylko dla v1 i tylko przy mapowaniu w ISA (jak `Mos6502Isa.Mem`). Przy tekście
   rejestru w `Octet` zepsułyby się `IsVolatile`/`Key`/`Sym`. Prymitywy 16-bit i rejestry żywe przez `call` wymagają zmian w
   selektorze. Brakuje uwagi o `PtrSetup` niszczącym DE (`Z80Isa.cs:122-123`, `Intel8080Isa.cs:130-131`).
5. Sekcja 2 pkt 2: „`At(sym, offset)`/`Loc` zwracają nazwę rejestru”: `At`/`Loc` są niewirtualne w `ByteIsa` (37-50) i przechodzą
   przez `Sym()` (`b` → `cc_r_b`). Mapowanie powinno siedzieć w prymitywach ISA, nie w `At`.
6. Sekcja 2 pkt 3: „komórki znikają z BSS jak przy ZP”: ZP nie znika, tylko zmienia segment. Dla rejestrów wystarczy segment `REG`,
   którego `PrintBss` nie drukuje (`ByteSelector.cs:809`). Nie wpływa to na `TargetSizeTests` (mierzy CODE).
7. Sekcja 3: „komórka lokalna funkcji (przedrostek `funkcja__`)”: błędne. Obejmuje lokalne `static` (`Lowering.cs:375-378`),
   zawodzi przy CaseFold i `cc_r_`, pomija inline'owane i tymczasowe modułowe. Zastąpić kryterium z pyt. 1.
8. Sekcja 3: „4 dopiero po `WideLegalizer` jako dwie połówki”: połówki to osobne symbole `x`/`x+2` przy jednym `Data` o rozmiarze 4
   (`WideLegalizer.cs:107-112`); alokator musi to jawnie obsłużyć.
9. Sekcja 3: „kopia parametru do lokalnej może być w rejestrze (`ld a,(cc_arg1)` `ld b,a`)”: lepiej `ld bc,(cc_arg1)` (4 B zamiast
   8 B). W liściach jeszcze lepiej całkowicie zaliasować parametr na `cc_argN` (−288 B na benchach).
10. Sekcja 3: „To jednocześnie rozwiązuje rekurencję”: poprawnie, ale parametry funkcji rekurencyjnych są zawsze w `Saved`
    (`Lowering.Frames.cs:159-162`), więc takie funkcje nic nie zyskują (fib 4%) aż do kroku 5.
11. Sekcja 4, krok 2 („tylko komórki 1-bajtowe w liściach, bez liveness”): zysk zmierzony to 34 B (1,3%). Wymóg „nieżywa na wejściu”
    to już liveness, bez której lokalne `static` psują program. Połączyć kroki 2–4 w jeden (liveness od początku).
12. Sekcja 4: „Kroki 1-3 są najtańsze i przynoszą największy udział zysku”: nieprawda. Tańsze i większe są optymalizacje modelu
    pamięci (sekcja c, druga tabela); powinny iść przed rejestrami.
13. Sekcja 5: „8080 nie ma `ld (nn),r`”: Z80 też nie ma. Dopisać prawdziwe braki 8080 (brak `ld rp,(nn)` dla BC/DE, `sbc hl`,
    `jr`, flagi V) oraz błąd emulatora (P jako przepełnienie w trybie 8080, `Z80Cpu.cs:214,225`).
14. Sekcja 6: „`ZeroPageAllocator` bez zmian, potrzebne `z:` i relokacja `Abs8`”: `Abs8` już jest. Brakuje form `z:` w `M6800Set`,
    `Mem` tylko dla mnemoników z trybem bezpośrednim (INC/DEC/TST go nie mają), `Size` dla `BranchRelaxer`, obszaru `C_ZP`
    i zerowania w crt0.
15. Sekcja 6: „akumulator B pomaga w łańcuchach 16-bit”: 6800 nie ma `adc`/`sbc`/`and`/`or` A z B (tylko `aba`/`sba`/`cba`), więc
    B nadaje się głównie na licznik 1-bajtowy.
16. Sekcja 6/koniec: „wspólny `FastStorageAllocator`”: zastąpić wspólnymi analizami (`IrFacts`, `IrLiveness`) i dwoma alokatorami
    (pyt. 9).
17. Sekcja 7: „154 → 90-100 B po krokach 1-4”: pomiar daje ok. 120 B dla samych rejestrów K=4. 55–60 B osiąga się dopiero razem
    z krokami z sekcji (c).
18. Sekcja 8: „testy `IrConformanceTests` i `TargetMatrixTests` bez zmian sprawdzają obie wersje”: przy kryterium ze szkicu
    `IrConformance` nie trafia w rejestry (komórki `c_a` są globalne). Obie wyrocznie są ślepe na błędy `Saved` (interpreter
    powtarza snapshot `Saved`, stub ma ten sam `Lowering`). Dopisać testy z oczekiwaniami ręcznymi i istniejący błąd `Frames`.
19. Sekcja 8: „flaga `--no-regs`”: niepotrzebna. `optimize`/`-O0` już dochodzi do `ByteTarget.Emit` i jest ignorowane
    (`ByteTarget.cs:54`); `IrProgram` testuje obie wartości.
20. Sekcja 9 pkt 3: „IXH/IXL działają w emulatorze `Z80Cpu`?”: nie, emulator nie obsługuje prefiksów DD/FD
    (`Z80Cpu.cs:510-526`).
