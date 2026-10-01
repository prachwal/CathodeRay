# Cele kompilatora mini-C

`cathode cc plik.c --cpu <cel>` kompiluje ten sam program na kilka procesorów. Front-end (lekser, parser, kontrola typów, obniżanie do
kodu pośredniego IR) nie zna procesora; cel drukuje IR jako asembler swojego CPU. Rejestr: `CTargets.All`.

| cel | CPU | bajty słowa | konwencja | interpreter w testach |
| --- | --- | --- | --- | --- |
| `stub` | zaślepka CathodeRay (A, X) | LE | argumenty w A/X i `cc_argN`, wynik w A/X | `StubCpu` |
| `6502`, `65c02`, `nes`, `6510` | MOS 6502 / WDC 65C02 / NES 2A03 / MOS 6510 | LE | argumenty `cc_argN`, wynik `cc_ret`, wskaźnik `(__p),Y` na stronie zerowej | `Mos6502` (tablice z JSON ISA) |
| `z80` | Zilog Z80 | LE | argumenty `cc_argN`, wynik W<=2 w HL (L dla 1 B; plan 35), HL jako rejestr adresowy | `Z80Cpu` |
| `8080` | Intel 8080 | LE | jak `z80` (wynik w HL), mnemoniki Intel | `Z80Cpu(intel8080: true)` |
| `6800` | Motorola 6800 | BE | argumenty `cc_argN`, wynik `cc_ret`, rejestr X | `Mc6800Cpu` (tablice z JSON ISA) |

Rozmiar kodu programów przykładowych na każdym celu: [target-sizes.md](target-sizes.md).

## Potok

```text
źródło C → CPreprocessor → Lexer → Parser → TypeChecker → Lowering (Wide8Legalizer → IrPasses) → IrInliner
        → cel.Emit:  CaseFold → Legalizer(wide) → WideLegalizer → Legalizer → [IndexFusion, strona zerowa: 6502]
                     → ByteSelector(ByteIsa) → Peephole/BranchRelaxer → tekst asemblera
        → asembler → linker (crt0 pierwszy, biblioteka na żądanie)
```

- **IR** (`Ir.cs`): trójadresowe operacje na komórkach W=1/2/4 (W=8 istnieje tylko do końca obniżania funkcji); `IrInterpreter` wykonuje go bez procesora i jest wyrocznią
  (`IrOracle`, `IrConformanceTests`).
- **`Legalizer`** zamienia operacje, których cel nie ma (mnożenie, dzielenie, przesunięcia o zmienną liczbę, duże bloki), na wołania funkcji z
  `stdlib/portable/rt_*.c` (mini-C kompilowane tym samym front-endem, linkowane raz na żądanie; dla 6502 i Z80 mnożenie i dzielenie są
  w asemblerze w `stdlib/target/<cel>/`).
- **`WideLegalizer`** rozbija komórki 32-bitowe na połówki 16-bitowe. **`Wide8Legalizer`** (`long long`) robi to samo z 64-bitowymi
  na połówki 32-bitowe, zanim ruszą przebiegi IR. `float` to zwykła komórka 32-bitowa: działania to wołania `__cc_f*` z `rt_float.c`.
- **`IrPasses`/`IrInliner`**: propagacja stałych i kopii, usuwanie martwych zapisów, wstawianie małych funkcji liściowych.
- **`CaseFold`** zmienia nazwy symboli różniących się tylko wielkością liter (asemblery Intela i Zilog ich nie rozróżniają).
- **`ByteSelector`** składa kod z prymitywów `ByteIsa` (A ← bajt, A ← A op bajt, bajt ← A, skoki, stos, wskaźnik) — to jedyna część zależna od CPU.

## Jak dodać procesor

1. **Asembler**: CPU musi mieć wpis w `AssemblerTargets` (plik ISA JSON + dialekt składni z `.segment`/`.global`/`.extern`/`.byte`/`.word`/`.res`
   albo odpowiednikami) oraz kolejność bajtów (`AssemblerTarget.Endianness`). Linker zna relokacje `Abs16`, `Rel8`, `Lo8`, `Hi8`.
2. **Prymitywy**: klasa `XxxIsa : ByteIsa` (~150 linii): `LoadA`/`StoreA`, `Alu` (Add/Sub/And/Or/Xor; `first` = pierwszy bajt łańcucha),
   `Cmp`, `ShlA`/`ShrA`, `Jump`/`JumpIf` (Zero/NotZero/Borrow/NoBorrow), `PushA`/`PopA`, `Call`/`CallIndirect`/`Return`,
   `PtrSetup`/`PtrLoad`/`PtrStore`, `Crt0()` oraz składnia danych (`Segment`, `Global`, `Extern`, `Bytes`, `Word`, `Reserve`).
   Zastrzeżone nazwy (rejestry, operatory asemblera) wpisz w `Reserved` — symbole użytkownika o takich nazwach dostają przedrostek `cc_r_`.
3. **Cel**: `XxxTarget : ByteTarget` (nazwa, `AssemblerCpu`, `ByteOrder`, `StackLimit`, `CreateIsa`) i wpis w `CTargets.All`.
4. **Crt0** ustawia stos, zeruje BSS (`__bss_start`..`__bss_end`), woła procedury z tablicy INIT, woła `main` i zatrzymuje CPU pętlą na sobie samej
   albo instrukcją HALT; definiuje `cc_arg1..6(+_h)`, `cc_ret(+_h)`, `cc_rethi`, `cc_retbuf`, `cc_t0`, `cc_t1` oraz pomocnika wołania pośredniego.
5. **Testy**: interpreter w `tests/` (`ICpuRunner`, rejestr `Runners`) — wtedy `IrConformanceTests`, `TargetMatrixTests` i testy
   funkcji (`CCastTests`, `CLongTests` …) uruchamiają się na nowym celu same. Odstępstwa od konwencji trzeba opisać w
   `stub-calling-conv.md`.
6. **Model**: wpis w `CpuModels` (rejestry, aliasy, `ClobberedByCall`, `Scratch`, `ResultReg`, `ArgRegs`, `PrimEffects`) —
   testy `CpuModel*Tests` pilnują zgodności z `CellRegisters`/`CellPairs` i emiterem.

## Kontrakt rejestrowy (`CpuModel`, plan 38)

Jawny model maszyny dla optymalizatora (`src/CathodeRay.C/Cpu*.cs`): rejestry i aliasy (`hl=[h,l]`),
co wołanie niszczy (`ClobberedByCall`), czym prymitywy drapią (`Scratch`, dawne `Clobbers`),
gdzie wraca wynik (`ResultReg`), dokąd idą argumenty (`ArgRegs`, dziś puste = ABI v1)
i efekty prymitywów (`PrimEffects`: czytane/zapisywane rejestry, flagi — sprawdzone fuzzem
`PrimEffectsFuzzTests` przeciw emulatorom, ~9k sekwencji). Selektor czyta model zamiast zgadywać
(`Taken` przez aliasy, `ArgCell`, walidacja w `AssignRegisters`).

## Ograniczenia celów bajtowych

- Komórki leżą w pamięci absolutnej (6502: najczęściej używane skalarne i wskaźniki trafiają na stronę zerową, prefiks `z:`), więc kod jest
  większy niż ręcznie pisany (miara: `docs/compare.md` — ok. 1,7× cc65 na 6502, ok. 3,6× SDCC na Z80).
- Skoki warunkowe 6502 i 6800 są relaksowane: krótki skok, gdy cel jest w zasięgu, inaczej odwrócony skok + `JMP`.
- `float`, `long long` i dzielenie/mnożenie 32-bitowe są programowe, więc wolne; `float` i `long long` na `stub` (24 KB kodu) mieszczą się
  tylko w małych programach.
