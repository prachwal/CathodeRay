# Architektura

## Układ repozytorium

```text
src/
  CathodeRay.Assembler/   asembler wielocelowy + linker modułów
  CathodeRay.C/           frontend Mini-C, kody pośrednie, cele, biblioteka standardowa
  CathodeRay.Cli/         polecenia `cathode asm | cc | link | stub ...`
  CathodeRay/             rdzeń wspólny (ALU, abstrakcje) i emulator CPU stub; emulatory pozostałych CPU są w testach
tests/                    testy jednostkowe i zgodności (wyrocznia IR, matryca celów)
samples/                  programy przykładowe (m.in. samples/bench dla porównań z cc65 i SDCC)
docs/                     opisy języka, celów, projektów i planów
tools/                    skrypty pomocnicze (Python)
data/                     opisy zestawów instrukcji w JSON
```

Zależności między projektami są jednokierunkowe: frontend C nie zależy od asemblera, a asembler nie zależy od emulatora.
Wspólne ustawienia (`net10.0`, `LangVersion=latest`) są w `Directory.Build.props`, a wersje pakietów w `Directory.Packages.props`.
Kod pilnują analizatory StyleCop i Roslynator.

## Potok kompilacji (`cathode cc`)

```text
źródło C
  → CPreprocessor → Lexer → Parser → TypeChecker
  → Lowering  (Wide8Legalizer → IrPasses) → IrInliner
  → cel.Emit:  CaseFold → Legalizer → WideLegalizer → Legalizer
               → [IndexFusion, strona zerowa: 6502]
               → ByteSelector (ByteIsa) → Peephole / BranchRelaxer → tekst asemblera
  → asembler → linker (crt0 pierwszy, biblioteka standardowa na żądanie)
```

Podział odpowiedzialności:

- **Frontend** (preprocesor, lekser, parser, kontrola typów, obniżanie do IR) nie zna procesora.
- **Cel** drukuje IR jako asembler swojego CPU. Jedyna część zależna od procesora to `ByteSelector` wraz z klasą `XxxIsa : ByteIsa`.
- **Legalizer** zamienia operacje, których cel nie ma (mnożenie, dzielenie, przesunięcia o zmienną liczbę, duże bloki),
  na wołania funkcji z `stdlib/portable/rt_*.c`. Są to programy Mini-C kompilowane tym samym frontendem i linkowane raz na żądanie.
  Na 6502 i Z80 mnożenie i dzielenie są napisane w asemblerze w `stdlib/target/<cel>/`.
- **WideLegalizer** rozbija komórki 32-bitowe na połówki 16-bitowe, a **Wide8Legalizer** (`long long`) robi to samo
  z komórkami 64-bitowymi, zanim ruszą przebiegi IR. `float` to zwykła komórka 32-bitowa, a działania na niej to wywołania `__cc_f*` z `rt_float.c`.
- **IrPasses / IrInliner**: propagacja stałych i kopii, usuwanie martwych zapisów, wstawianie małych funkcji liściowych.
- **CaseFold** zmienia nazwy symboli różniących się tylko wielkością liter, bo asemblery Intela i Zilog ich nie rozróżniają.

## Kody pośrednie

### Cell IR (produkcyjny)

Trójadresowe operacje na komórkach pamięci o szerokości 1, 2 lub 4 bajtów (szerokość 8 istnieje tylko do końca obniżania funkcji).
Komórki są absolutne, co upraszcza obniżanie, interpretację i zgodność z istniejącymi celami, ale nie modeluje jawnie presji rejestrów.
`IrInterpreter` wykonuje Cell IR bez procesora i jest wyrocznią semantyczną.

### VReg IR (alternatywny, wybierany flagą)

Rejestry wirtualne niezależne od celu, alokowane dopiero po optymalizacjach niezależnych od celu. Opada z powrotem do Cell IR,
dzięki czemu cała istniejąca ścieżka (ABI, crt0, wywołania ogonowe) działa bez zmian. Szczegóły: [optimization.md](optimization.md).
Wybór ścieżki: `cathode cc ... --ir cell|vreg`; domyślny jest `cell`.

## Biblioteka standardowa

Podzbiór `string.h`, `ctype.h`, `stdlib.h` i `stdio.h`. Linkowanie jest selektywne: do programu trafiają tylko używane funkcje.
Biblioteka jest częściowo przenośna (Mini-C w `stdlib/portable/`), a częściowo napisana w asemblerze dla konkretnego celu.

## Informacje diagnostyczne

Jedyna informacja debugowa to plik mapy (`--map`). Nie ma DWARF ani sanitizerów. `--stats` wypisuje rozmiar segmentu CODE po linkowaniu.

## CLI

| Polecenie | Zastosowanie |
| --- | --- |
| `cathode asm plik.s -o plik.bin --cpu z80` | asemblacja (`-f bin\|hex\|obj`, `-l` listing, `--map NAZWA@adres`) |
| `cathode cc plik.c -o plik.bin --cpu stub\|6502\|65c02\|z80\|8080\|6800` | kompilacja Mini-C (`--ir`, `--map`, `--stats`) |
| `cathode link ...` | linkowanie modułów obiektowych z plikiem `.cfg` |
| `cathode stub run ...` | uruchomienie na emulatorze stub (m.in. dekoder ekranu `--screen-at`) |
