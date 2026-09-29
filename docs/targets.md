# Cele kompilatora mini-C

`cathode cc plik.c --cpu <cel>` kompiluje ten sam program na kilka procesorów. Front-end (lekser, parser, kontrola typów, obniżanie do
kodu pośredniego IR) nie zna procesora; cel drukuje IR jako asembler swojego CPU. Rejestr: `CTargets.All`.

| cel | CPU | bajty słowa | konwencja | interpreter w testach |
| --- | --- | --- | --- | --- |
| `stub` | zaślepka CathodeRay (A, X) | LE | argumenty w A/X i `cc_argN`, wynik w A/X | `StubCpu` |
| `6502`, `65c02` | MOS 6502 / WDC 65C02 | LE | argumenty `cc_argN`, wynik `cc_ret`, wskaźnik `(__p),Y` na stronie zerowej | `Mos6502` (tablice z JSON ISA) |
| `z80` | Zilog Z80 | LE | j.w., HL jako rejestr adresowy | `Z80Cpu` |
| `8080` | Intel 8080 | LE | j.w., mnemoniki Intel | `Z80Cpu(intel8080: true)` |
| `6800` | Motorola 6800 | BE | j.w., rejestr X | `Mc6800Cpu` (tablice z JSON ISA) |

Rozmiar kodu programów przykładowych na każdym celu: [target-sizes.md](target-sizes.md).

## Potok

```
źródło C → CPreprocessor → Lexer → Parser → TypeChecker → Lowering (Ir.Module) → IrPasses
        → cel.Emit:  Legalizer(wide) → WideLegalizer → Legalizer → ByteSelector(ByteIsa) → tekst asemblera
        → asembler → linker (crt0 pierwszy, biblioteka na żądanie)
```

- **IR** (`Ir.cs`): trójadresowe operacje na komórkach W=1/2/4; `IrInterpreter` wykonuje go bez procesora i jest wyrocznią
  (`IrOracle`, `IrConformanceTests`).
- **`Legalizer`** zamienia operacje, których cel nie ma (mnożenie, dzielenie, przesunięcia o zmienną liczbę, duże bloki), na wołania funkcji z
  `stdlib/portable/rt.c` (mini-C kompilowane tym samym front-endem).
- **`WideLegalizer`** rozbija komórki 32-bitowe na połówki 16-bitowe.
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

## Ograniczenia celów bajtowych

- Komórki leżą w pamięci absolutnej (6502: bez strony zerowej poza wskaźnikiem `__p`), więc kod jest większy niż ręcznie pisany.
- Skoki warunkowe 6502 i 6800 to krótki skok odwrócony + `JMP` (bez ograniczenia zasięgu).
- Nazwy symboli w asemblerach Intela i Zilog nie rozróżniają wielkości liter: identyfikatory C różniące się tylko wielkością liter kolidują.
