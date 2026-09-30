# Analiza planu 37 (VReg) i folderu `src/CathodeRay.C/VReg/`

Stan: gałąź `vreg/design`, HEAD `118dc64` (plan 37 kroki 1-2 zrobione), w drzewie roboczym niezacommitowane `VRegFacts.cs`,
`VRegLiveness.cs`, `VRegPasses.cs` i testy (krok 3 w toku, inna sesja). Analiza tylko czyta; nie zmienia kodu.

## 1. Co jest zweryfikowane

- `--ir vreg` w CLI nadal kończy się błędem „not yet implemented” (zgodnie z projektem do kroku 4); potok VReg działa na poziomie biblioteki
  (`VRegPipeline.Emit`: Lower → `VRegLift` → `VRegToCell` → istniejący cel).
- 29 testów `VReg*` zielonych na czystym worktree `118dc64`.
- **Roundtrip Cell → VReg → Cell jest bajt-identyczny szerzej niż w teście planu:** 624 porównania tekstu asemblera (wszystkie `samples/minic` i
  `samples/bench` × 6 celów × `optimize` true/false), 0 różnic, 0 błędów. Test w repo sprawdza 8 programów.
- Decyzja D1 (VReg opada do Cell IR, ABI za darmo) jest dobra: ABI, ramki, wywołanie ogonowe i mapa rejestrów celu nie są duplikowane.

## 2. Problemy, które trzeba rozstrzygnąć (od najpoważniejszych)

1. **Żywość zabija każdym zapisem; projekt i `IrLiveness` mówią co innego.** `VRegLiveness` (`kills[i] = "r" + def.Id`, komentarz „rejestry są całe z konstrukcji”)
   zabija rejestr przy każdym zapisie. `IrLiveness.Killed` zabija tylko gdy `def.W == cellSize(def.Sym)` i symbol nie ma `+`; projekt §5 też mówi „zapis
   zabija tylko przy pełnym pokryciu W”. Rejestr w `VRegLift` jest kluczowany pełnym symbolem komórki, a tymczasowe `f__t@N` są używane z różnymi
   szerokościami (1, 2, 4). Zapis W1 do `t@N` zabiłby więc rejestr W2 i `Saved` wyliczone z tej żywości byłoby za małe: to ta sama klasa błędu co regresja
   `646d204` (zły wynik rekurencji). Wymagane przed krokiem 4: reguła pokrycia `W` jak w `IrLiveness` i testy: zapis W1 do komórki W2 żywej po wołaniu,
   połówki `x`/`x+2`, pętla z wołaniem.
2. **Nieznana etykieta `__end`.** `VRegLift` daje ostatniemu blokowi zakończonemu `Br` spadek `"__end"` (gdy nie ma następnego bloku), a
   `VRegLiveness.Of` wywołuje `Target(branch.Else)`, które rzuca `InvalidOperationException`, bo `__end` nie jest w mapie etykiet. Do sprawdzenia testem:
   ciało kończące się `BrCmp` (np. `do { } while (x)` jako ostatnia instrukcja `void`). Poprawka: `__end` = indeks za ostatnią instrukcją.
3. **Komórki z wziętym adresem i `volatile` są rejestrami.** `VRegLift` przypina (`Pinned`) tylko agregaty z `Saved`. Skalar, którego adres jest brany
   (`AddrOf(x)` + `Load`/`Store` przez wskaźnik), nadal jest rejestrem `x`. Dla roundtripu to bez znaczenia (wraca ta sama komórka), ale każdy alokator
   (krok 4/7) zepsułby aliasowanie. Projekt (§2) wymaga slotu, nigdy vrega; `Module.Volatile` też musi wykluczać rejestr. To ma być test i kod przed alokatorem.
4. **Krok 4 nic nie zmienia, krok 7 wymaga pracy poza planem.** `AccumulatorAllocator` ma generować kod „równoważny Cell” (cel: test różnicowy), więc zysk
   zero z konstrukcji. Zysk ma dać dopiero `LinearScanAllocator` (krok 7), a mapę „komórka → rejestr” mają dziś tylko `Z80Isa` i `Intel8080Isa`
   (`AssignRegisters`/`CellRegisters`); `Mos6502Isa` i `M6800Isa` jej nie mają. Plan nie zawiera pracy ISA (X/Y na 6502, B/X na 6800), a to ona jest kosztem.
   Dla Z80/8080 istniejący `RegisterAllocator` już daje wynik bliski SDCC, więc VReg nic tam nie doda bez dzielenia zakresów życia.
5. **VReg z `VRegLift` nie jest SSA ani nie dzieli zakresów.** Rejestr = komórka pod inną nazwą, czyli ta sama informacja co Cell IR. Projekt §3 zakładał
   „świeży vreg na każdy wynik”, czego podnoszenie z Cell IR nie daje. Zysk ponad dzisiejszy `RegisterAllocator` pojawi się dopiero po przebiegach
   (kopie, martwe rejestry, lokalne CSE, podział zakresów); `VRegPasses.cs` jest w toku, ale żaden krok planu nie ma mierzalnego kryterium zysku.
6. **Brak bramki „opłaca się”.** Plan nie mówi, przy jakim wyniku porzucamy podejście. Proponuję: po kroku 4 i przebiegach (nie dopiero po 7) zmierzyć
   `--ir vreg` kontra `cell` na `samples/bench` i na tabeli rozmiarów; jeśli zysk < 3% na 6502/6800, zatrzymać plan przed krokiem 7.

## 3. Co realnie zostaje do zyskania (pomiar `docs/compare.md`, `docs/disasm-*.md`)

Średnia geometryczna mini-C / cc65 = 1,34 (6502), mini-C / SDCC = 1,36 (Z80). Największe rozbieżności to wywołania i rekurencja: `fib` 6502 144 B vs 74 B
(1,95×), `sw` 120 vs 57 (2,1×), `add32` 46 vs 22, `div16` 92 vs 44 (2,1×), `fib` Z80 78 vs 31 (2,5×). Ich przyczyny to ABI (wynik w `cc_ret` w pamięci na 6502/6800),
brak użycia X/Y na 6502 i brak wywołania ogonowego tam, czyli nie brak alokatora. VReg tego nie naprawi, dopóki 6502 nie dostanie wyniku w A/X i prymitywów
rejestrowych w ISA (odpowiednik planów 35-36 dla Z80).

## 4. Rekomendowana kolejność

1. Krok 3 (`VRegFacts`/`VRegLiveness`) z poprawkami z pkt 2.1-2.3 i kopiami regresji goto-po-wołaniu (=39) i pętli z wołaniem (=19) jako testy jednostkowe.
   Wykonawca: mocny model (liveness to miejsce, gdzie popełniono `646d204`); test różnicowy z `gcc` przepuścić przez `--ir vreg` (parametr `IR` w `CcRun`/fuzzach).
2. Przed krokiem 4: mały eksperyment ilościowy (skrypt na listingach 6502: ile komórek ma zakres życia w obrębie bloku i mieści się w A/X/Y), żeby ocenić sufit
   zysku bez pisania alokatora. Wynik do `docs/`.
3. Równolegle, niezależnie od VReg: wynik W<=2 w A/X na 6502 (analogia planu 35 kroku 2) oraz wywołanie ogonowe na 6502; to bezpośrednio uderza w największe
   luki (`fib`, `sw`, `add32`).
4. Kroki 4-5 dopiero po punktach 1-2; krok 6 (flagi `nes`/`ioPort`) jest niezależny od VReg i można go robić teraz (Haiku, jeden plik + test).
5. Krok 7-8 tylko po bramce z pkt 2.6.

## 5. Higiena planu i procesu

- Plan 37 ma status „w kolejce”, choć 2/8 zrobione, a krok 3 jest w drzewie roboczym; hygiene dopuszcza jeden plan otwarty, więc status wymaga korekty.
- W tym samym drzewie (`vreg/design`) pracuje inna sesja; zmiany niezacommitowane. Przed uruchomieniem czegokolwiek w tym drzewie uzgodnić, kto je buduje.
- Lekcje z planów 32-36 dla wykonawców Haiku: akceptacja przez listing rzeczywistego programu, nie tylko testy jednostkowe na tekstach syntetycznych;
  mutacja kontrolna obowiązkowa; raport agenta nie jest dowodem.
