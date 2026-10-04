# Testy i sposób pracy

## Strategia testów

Testy są w xUnit + FluentAssertions. Końcowy runner sharduje kosztowne macierze target/VReg oraz zgodność IR po celu, bez pomijania żadnego celu ani trybu optymalizacji.
Warstwy, od najtańszej:

| Warstwa | Co sprawdza | Gdzie |
| --- | --- | --- |
| Wyrocznia IR | `IrInterpreter` wykonuje IR bez procesora, a wynik programu musi zgadzać się z wykonaniem na emulatorze celu | `IrOracle`, `IrConformanceTests`, shardy `CATHODERAY_IR_TARGET` |
| Matryca celów | ten sam program na każdym celu (wartość i konsola) | `TargetMatrixTests`, `VRegMatrixTests` (26 programów × 5 celów) |
| Dokumentacja jako test | przykłady w `minic.md` (bloki ` ```c expect=N `) są kompilowane i uruchamiane | `MiniCDocsTests` |
| Goldeny rozmiaru kodu | bramka przed regresją: tabela bajtów na program i cel | `TargetSizeTests` → `docs/target-sizes.md`, `VRegSizeTests` → `docs/vreg-sizes.md` |
| Goldeny asemblera | bajt w bajt z ca65/ld65, z80asm, as6800 | `tests/.../Asm`, `Link`, `Cond`, `Locals`, `Macros`, `Segments`, `Modules`, `Scopes`, `Longtail` |
| Spójność modelu CPU | model rejestrowy zgodny z alokatorem i emiterem | `CpuModelConsistencyTests`, `CpuModelAllocatorTests` |
| Fuzz różnicowy | efekty prymitywów vs emulator (ok. 9 tys. sekwencji, seed 38) | `PrimEffectsFuzzTests`; skrypty `leaf_fuzz.py`, `recursion_fuzz.py` |

Zasada praktyczna: zmiana w generatorze kodu nie może zwiększyć rozmiaru w goldenach bez świadomej decyzji.
Tabele odświeża się jawnie, np. `UPDATE_TARGET_SIZES=1 dotnet test --filter TargetSizeTests`.

## Kanoniczny flow (`tools/test_gate.py`)

```bash
python3 tools/test_gate.py changed [--base main]  # build + testy dobrane do diffu
python3 tools/test_gate.py changed --dry-run      # pokaż dobór bez uruchamiania
python3 tools/test_gate.py smoke                  # 5 seedów leaf + recursion
python3 tools/test_gate.py goldens                # target-sizes + vreg-sizes, bez UPDATE
python3 tools/test_gate.py full                   # jedyna końcowa bramka
```

`changed` mapuje znane pliki na klasy i cele; nierozpoznana zmiana kodu bezpiecznie uruchamia `full`.
Macierze target/VReg i testy IR są dzielone po celach i uruchamiane maksymalnie czterema procesami naraz. `full` buduje z `-warnaserror`,
uruchamia oba fuzzery po 60 seedów, macierze i pełną macierz IR w shardach, resztę suity raz, a na końcu plan hygiene i kontrolę diffu.
Po zielonym `full` nie zmieniaj kodu produkcyjnego; jeśli to nastąpi, uruchom `full` ponownie. Zmiany docs lub generatora fuzz
wymagają testów celowanych i kontroli dokumentacji, nie powtórzenia całej suity.

## Narzędzia pomocnicze (`tools/`)

| Skrypt | Zastosowanie |
| --- | --- |
| `compare.py` | porównanie rozmiaru kodu z cc65 i SDCC (`--write` odświeża `docs/compare.md`) |
| `disasm_compare.py`, `hotspots.py` | porównanie listingów, wykrywanie gorących miejsc |
| `make_*_golden.py` | generowanie wzorców z oryginalnych asemblerów |
| `make_z80_isa.py`, `make_6800_isa.py`, `import_isa_json.py` | budowa danych ISA |
| `plan.py`, `plan_mcp.py` | obsługa planów (CLI i serwer MCP) |
| `mcp_instructions.py` | serwer MCP z danymi o instrukcjach procesorów |

## Praca planami

Całość rozwoju jest prowadzona w planach w `docs/plans/` (obecnie 43). Każdy plan to para plików:
`NN-nazwa.json` (źródło prawdy: tytuł, lista zadań, status) i `NN-nazwa.md` (wersja do czytania generowana z JSON).

- Zadanie ma rozmiar S, M lub L, jasne pliki, których dotyka, i kryterium akceptacji („kiedy jest zrobione”).
  Typowo jest to jedna zmiana, jeden test i jeden commit.
- Po wykonaniu zadanie dostaje wpis z krótkim opisem wyniku, a plan liczy postęp (np. „8/8 gotowych”, „1/10 gotowych”).
- Tematyka planów: 01–13 asembler i linker, 14–16 stub, 17–31 frontend i generator Mini-C, 32–36 wydajność i ABI,
  34 maszyna wirtualna (bytecode, jeszcze w kolejce), 37 VReg, 38 CpuModel, 39 ABI v2 (pilot `nes`), 40–43 Z80/8080 (40–42 closed, 43 w kolejce).
- Agent `plan-coder` (`.claude/agents/plan-coder.md`) wykonuje dokładnie jedno zadanie rozmiaru S z planu, uruchamia testy,
  aktualizuje plan i robi jeden commit. Hook `plan-coder-guard.py` pilnuje, żeby nie wykraczał poza zakres.
- Pomiary (rozmiar kodu, porównania z cc65 i SDCC) są częścią zadań zamykających plan. Decyzje o większych krokach (SSA, alokator grafowy)
  zapadają dopiero na podstawie liczb.

## Pozostałe narzędzia

```bash
cathode cc samples/bench/fib.c -o fib.bin --cpu z80
python3 tools/compare.py --write                 # wymaga cc65 i SDCC
```
