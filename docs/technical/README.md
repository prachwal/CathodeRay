# CathodeRay — dokumentacja techniczna

Zbiorcze opracowanie na podstawie istniejących dokumentów w `docs/` i planów w `docs/plans/`.
Nie zastępuje ich: każdy plik wskazuje dokument źródłowy, w którym są szczegóły.

CathodeRay to wielocelowy asembler i kompilator Mini-C napisany w C# (.NET 10). Cele: Z80, Intel 8080,
MOS 6502 / 65C02 (warianty `nes`, `6510`), Motorola 6800 oraz edukacyjny CPU `stub`.

## Spis

| Plik | Zawartość |
| --- | --- |
| [architecture.md](architecture.md) | Układ repozytorium, potok kompilacji, kody pośrednie (Cell IR, VReg IR), CLI |
| [targets-and-abi.md](targets-and-abi.md) | Cele, konwencja wołań, układ pamięci, `CpuModel`, dodawanie nowego CPU |
| [assembler-and-linker.md](assembler-and-linker.md) | Asembler (dialekty, dyrektywy), format obiektu, linker, segmenty |
| [optimization.md](optimization.md) | Stan optymalizacji, VReg i alokacja rejestrów, wyniki pomiarów, luki |
| [testing-and-process.md](testing-and-process.md) | Strategia testów (wyrocznie, matryce, goldeny, fuzz) i sposób pracy planami |

## Dokumenty źródłowe

- Język: [../minic.md](../minic.md) (przykłady są kompilowane i uruchamiane w testach)
- Cele i potok: [../targets.md](../targets.md), [../stub-calling-conv.md](../stub-calling-conv.md)
- Asembler: [../z80-assembler.md](../z80-assembler.md), [../assembler-capability-gaps.md](../assembler-capability-gaps.md), [../linker-segments.md](../linker-segments.md)
- Optymalizacja: [../minic-optimization.md](../minic-optimization.md), [../vreg-design.md](../vreg-design.md), [../ir-vreg-plan.md](../ir-vreg-plan.md)
- Pomiary: [../compare.md](../compare.md), [../target-sizes.md](../target-sizes.md), [../vreg-sizes.md](../vreg-sizes.md), [../vreg-alloc.md](../vreg-alloc.md)
- Plany: [../plans/](../plans/) (pliki `NN-nazwa.json` jako źródło prawdy i `NN-nazwa.md` jako wersja do czytania)

## Uwaga o aktualności

Liczby w tym folderze pochodzą z dokumentów źródłowych w momencie ich ostatniej aktualizacji. Tabele rozmiarów
są generowane testami i skryptami (patrz [testing-and-process.md](testing-and-process.md)), więc po zmianach
w generatorze kodu mogą się rozjechać z kodem do czasu ich odświeżenia.
