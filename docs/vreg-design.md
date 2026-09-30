# VReg dla obecnego rozwiązania — projekt techniczny

Status: **projekt** (nie zaimplementowany). Branch: `vreg/design`.
Punkt wyjścia: [ir-vreg-plan.md](ir-vreg-plan.md#goal) (propozycja ogólna).
Ten dokument rozstrzyga kwestie, które tamten zostawia otwarte, i wiąże je z konkretnym kodem.

## 0. Mapa obecnego potoku (kotwice)

```text
.c → CPreprocessor → Lexer → Parser → TypeChecker → CheckedProgram
  → Codegen.Lower ──→ Ir.Module (komórki absolutne)
  → IrPasses → Legalizer → ParamAlias → RegisterAllocator → ByteSelector → ByteIsa → asm
  → assembler → linker (+ crt0, stdlib) → .bin
```

| Element | Kotwica |
|---|---|
| Wejście potoku (`CheckedProgram → Ir.Module`) | [Codegen.cs](../src/CathodeRay.C/Codegen.cs#L24-L38) |
| Kształt Cell IR (Op/Ins/Function) | [Ir.cs](../src/CathodeRay.C/Ir.cs#L96-L229) |
| Przebiegi (`Optimize`) | [IrPasses.cs](../src/CathodeRay.C/IrPasses.cs#L12) |
| Legalizacja (+ soft-float/long) | [Legalizer.cs](../src/CathodeRay.C/Legalizer.cs#L36) |
| Rejestr celów (`--cpu`) | [CTargets.cs](../src/CathodeRay.C/CTargets.cs#L7-L18) |
| Kontrakt celu | [ICTarget.cs](../src/CathodeRay.C/ICTarget.cs#L9-L31) |
| Przydział rejestrów komórek | [RegisterAllocator.cs](../src/CathodeRay.C/RegisterAllocator.cs#L21) |
| Selektor → asm | [ByteSelector.cs](../src/CathodeRay.C/ByteSelector.cs#L38) |
| Prymitywy ISA celu | [ByteIsa.cs](../src/CathodeRay.C/ByteIsa.cs#L38-L48) |
| Żywość na grafie przepływu | [IrLiveness.cs](../src/CathodeRay.C/IrLiveness.cs#L24-L89) |
| Ramki rekurencji (`Saved`) | [Lowering.Frames.cs](../src/CathodeRay.C/Lowering.Frames.cs#L33-L52) |
| Oracle semantyczny (testy) | [IrOracle.cs](../tests/CathodeRay.Tests/IrOracle.cs) |
| Interpreter Cell IR | [IrInterpreter.cs](../src/CathodeRay.C/IrInterpreter.cs#L8-L46) |
| Flagi CLI (`cc`) | [CcCommand.cs](../src/CathodeRay.Cli/CcCommand.cs#L29-L33) |
| Bramka rozmiarów | [TargetSizeTests.cs](../tests/CathodeRay.Tests/TargetSizeTests.cs#L36-L45) |
| ABI (zamrożone) | [stub-calling-conv.md](stub-calling-conv.md#funkcje), [ramki](stub-calling-conv.md#ramki-plan-20-callee-saves-na-stosie-sprzętowym) |
| Cele / dodawanie CPU | [targets.md](targets.md#potok), [targets.md](targets.md#jak-dodać-procesor), [targets.md](targets.md#ograniczenia-celów-bajtowych) |

## 1. Decyzje architektoniczne

### D1. VReg opada do Cell IR (adapter, nie drugi backend)
Po alokacji moduł VReg jest przepisywany na `Ir.Module`: każdy przydzielony vreg staje się
syntetyczną komórką (`%t3` → `fn__vr3`), a mapa rejestrów idzie istniejącą ścieżką
[AssignRegisters](../src/CathodeRay.C/ByteIsa.cs#L207-L217).
Efekt: **całe ABI działa za darmo** — `cc_argN`, wynik w HL
([ByteIsa.cs](../src/CathodeRay.C/ByteIsa.cs#L276-L303)),
[tail calls](../src/CathodeRay.C/ByteSelector.cs#L792-L831),
`callSave`, crt0. Alternatywa (własny emit vreg→asm) oznaczałaby duplikację ABI,
a ABI jest „żywym organizmem" ([minic-optimization.md](minic-optimization.md#ecosystem-limitations)).
Szczegóły: [§4](#4-adapter-vregcell).

### D2. Spill statyczno-komórkowy na 8 bitach
6502 nie ma adresowania względem wskaźnika stosu, stub/6800 podobnie —
[ograniczenia celów](targets.md#ograniczenia-celów-bajtowych).
Spill vrega to **statyczna komórka absolutna** (`fn__spN`, ew. strona zerowa na 6502),
nie „stack slot". Ramka w sensie sprzętowym powstaje tylko na przyszłych celach 16/32-bitowych.
Rekurencja dalej działa przez [Saved](../src/CathodeRay.C/Lowering.Frames.cs#L113-L136):
zbiór `Saved` syntetycznego modułu = vregi żywe po wołaniach (z analizy §5) + parametry + AddrOf.

### D3. Reuse zamiast pisania od nowa
- Żywość: adaptacja [IrLiveness.Of](../src/CathodeRay.C/IrLiveness.cs#L24-L58)
  (sukcesory `Jmp`/`BrCmp`/`Ret`, punkt stały) na bloki VReg zamiast listy instrukcji.
- Uses/Def: [IrFacts.cs](../src/CathodeRay.C/IrFacts.cs) jako wzorzec dla `VRegFacts`.
- Pierwszy alokator konserwatywny = istniejący
  [RegisterAllocator.Run](../src/CathodeRay.C/RegisterAllocator.cs#L21) jako fallback/porównanie.
- Oracle: [IrOracle.cs](../tests/CathodeRay.Tests/IrOracle.cs) żyje w testach —
  decyzja: `VRegOracle` też w testach (nie wyciągamy oracle do `src`).

### D4. Fabryka dopiero z drugą implementacją
Faza 0 to **tylko flaga** `--ir cell|vreg` w [CcCommand.cs](../src/CathodeRay.Cli/CcCommand.cs#L32-L33)
plus `vreg → czytelny błąd`. Interfejs `IIrPipeline` z
[ir-vreg-plan.md §5.2](ir-vreg-plan.md#52-factory-c-sketch) powstaje, gdy istnieje kod wołający go
z dwóch stron; wcześniej to scaffolding. Bez `object` w sygnaturach — generyki
(`IIrPipeline<TModule>`).

### D5. Minimalny zestaw instrukcji MVP (bez phi, bez copyblock)
`mov / bin / un / load / store / cmp / br / jmp / call / ret`.
`phi` wraca w fazie SSA, `copyblock/fill` nie istnieją — structy rozpadają się na pola
przy loweringu (jak dziś w [Lowering.Memory.cs](../src/CathodeRay.C/Lowering.Memory.cs)).
`loadidx/storeidx` tylko jeśli selektor 6502 ich potrzebuje (`(__p),Y`).

### D6. Warianty CPU przez rejestr, nie nową fabrykę
[CTargets](../src/CathodeRay.C/CTargets.cs#L7) już wariantuje:
`6502` vs `65c02` to flaga w jednej klasie
([Mos6502Target.cs](../src/CathodeRay.C/Mos6502Target.cs#L11)).
NES (2A03: brak BCD) i 6510 (port I/O `$00/$01`, inna mapa) wchodzą tak samo:
`new Mos6502Target(nes: true)` / `(ioPort: true)` — ten sam selektor, inny `Layout`/`Crt0`,
flaga gasząca ścieżki BCD. Alokator VReg czyta klasę celu z `ICTarget`
([§6](#6-alokator-i-cele)).

## 2. Kształt VRegModule

```csharp
// VRegModule.cs — lustro Ir.cs#L96-L229, ale rejestry zamiast komórek.
public static class VReg
{
    public sealed record Reg(int Id, int W);          // %tN, W = 1|2|4
    public sealed record Imm(int Value, int W);
    public sealed record Addr(string Sym, int Off);   // global / funkcja / slot spillu
    public abstract record Op;
    public abstract record Ins;
    public sealed record Mov(Reg Dst, Op Src) : Ins;
    public sealed record Bin(Ir.BinOp Kind, Reg Dst, Op A, Op B) : Ins;   // reuse enumów z Ir
    public sealed record Un(Ir.UnOp Kind, Reg Dst, Op A) : Ins;
    public sealed record Load(Reg Dst, Addr Ptr, int Bytes) : Ins;
    public sealed record Store(Addr Ptr, Op Value, int Bytes) : Ins;
    public sealed record Cmp(Ir.Cond C, Reg Dst, Op A, Op B) : Ins;       // Dst = i8 0/1
    public sealed record Br(Reg C, string Then, string Else) : Ins;
    public sealed record Jmp(string Target) : Ins;
    public sealed record Call(string? Direct, Reg? Indirect, IReadOnlyList<Op> Args,
        IReadOnlyList<int> ParamWidths, Reg? Result, int RetW) : Ins;
    public sealed record Ret(Op? Value, int W) : Ins;
    public sealed record Block(string Label, IReadOnlyList<Ins> Code);
    public sealed record Function(string Name, bool IsStatic, IReadOnlyList<Reg> Params,
        int RetW, IReadOnlyList<Block> Blocks);
}
```

Zasady: bloki z jawnymi etykietami (wejście dla SSA i [IrLiveness](../src/CathodeRay.C/IrLiveness.cs#L44-L50)
bez zmian kształtu), szerokości `1|2|4` jak `uchar/int/long`, adresy tylko przez `Addr`
(adres-wzięty lokal od razu dostaje slot, nigdy vrega — por.
[LiveAcrossCalls](../src/CathodeRay.C/Lowering.Frames.cs#L39-L49)).

## 3. Lowering AST → VReg

Start od [Lowering.Expressions.cs](../src/CathodeRay.C/Lowering.Expressions.cs) (struktura),
ale zamiast emisji do komórek: świeży vreg na każdy wynik (`%t = bin …`).
Kryteria kawałka: skalary, `if/while/for` (bloki + `Br`), wołania bezpośrednie.
Poza MVP: `goto` (krawędzie jak `Jmp` — uwaga na regresję
[646d204](https://github.com/prachwal/CathodeRay/commit/646d204): skok w przód po wołaniu),
wskaźniki funkcyjne, structy, `long`/`float` (zostają na Cell do fazy 4).

## 4. Adapter VReg→Cell

1. Alokator zwraca `vreg → PhysReg | SpillSlot` ([§6](#6-alokator-i-cele)).
2. Każdy vreg → syntetyczna komórka `fn__vr{id}` (`Ir.Owned`, rozmiar = `W`).
   Spillowane — zwykłe komórki w sekcji danych; przydzielone do rejestru —
   komórki z wpisem w mapie [AssignRegisters](../src/CathodeRay.C/ByteIsa.cs#L207-L212)
   (mechanizm już istnieje dla komórek „w rejestrze").
3. Instrukcje VReg → 1:1 na `Ir.Ins` (`Mov→Mov`, `Bin→Bin`, `C.js#L120-L214` analogicznie;
   `Br→BrCmp`, bloki → `Label`).
4. `Saved` modułu: vregi żywe po którymś `Call` (analiza z §5) + parametry + sloty AddrOf —
   dokładnie reguła [LiveAcrossCalls](../src/CathodeRay.C/Lowering.Frames.cs#L33-L52),
   liczona na VReg przed adaptacją (symbole bazowe bez sufiksu połówki, jak
   [BaseSymbol](../src/CathodeRay.C/IrLiveness.cs#L95-L98)).
5. Dalej istniejący potok bez zmian: [IrPasses](../src/CathodeRay.C/IrPasses.cs#L12) →
   [Legalizer](../src/CathodeRay.C/Legalizer.cs#L36) →
   [ByteSelector](../src/CathodeRay.C/ByteSelector.cs#L38).

## 5. Żywość i przebiegi VReg

- `VRegFacts`: Uses/Def na wzór [IrFacts.cs](../src/CathodeRay.C/IrFacts.cs).
- `VRegLiveness`: ten sam punkt stały co
  [IrLiveness.Of](../src/CathodeRay.C/IrLiveness.cs#L60-L89), sukcesory z krawędzi bloków
  (`Br` → dwa cele, `Jmp` → jeden, `Ret` → zero). Zapis zabija tylko przy pełnym pokryciu `W`.
- Przebiegi w kolejności: fold stałych → martwe vregi → lokalne CSE w bloku → liveness →
  alokacja. Kolejność jak [IrPasses](../src/CathodeRay.C/IrPasses.cs#L12), żeby wyniki były porównywalne.

## 6. Alokator i cele

```csharp
public interface IVRegAllocator
{
    // vreg → rejestr fizyczny ("a", "hl", "bc"…) albo null (= spill do komórki).
    IReadOnlyDictionary<int, string?> Allocate(VReg.Function f, VRegTargetInfo target);
}
public sealed record VRegTargetInfo(IReadOnlyList<string> PhysRegs, bool HasPairs, int MaxRegs);
```

- `AccumulatorAllocator` (MVP, cel zgodności): wszystko spilluje, trzyma bieżącą wartość w A —
  **ma generować kod równoważny Cell**, co daje test różnicowy za darmo.
- `LinearScanAllocator`: później, gdy liveness + spill stabilne; pary (`bc/de/hl`) z
  [CellPairs](../src/CathodeRay.C/ByteIsa.cs#L48).
- Dane o celu pochodzą z `ICTarget` ([§D6](#d6-warianty-cpu-przez-rejestr-nie-nową-fabrykę));
  żadnych `if (cpu == ...)` w alokatorze — tylko `VRegTargetInfo`.

## 7. CLI

W [CcCommand.cs](../src/CathodeRay.Cli/CcCommand.cs#L32-L33) obok `--cpu`:

```text
--ir cell|vreg      (default: cell; vreg → błąd do fazy 3)
--ir list           (wypisz rodzaje, wyjdź; odpowiednik CTargets.Find dla IR)
```

Parser rodzaju ląduje przy `CTargets`-podobnym rejestrze dopiero w fazie z dwoma potokami ([§D4](#d4-fabryka-dopiero-z-drugą-implementacją)).

## 8. Testy i bramki

| Poziom | Co | Kotwica wzorca |
|---|---|---|
| Interpreter VReg | `VRegInterpreter` wykonuje moduł bezpośrednio | [IrInterpreter.cs](../src/CathodeRay.C/IrInterpreter.cs#L8) |
| Oracle | `VRegOracle`: wynik `main` + efekty = Cell | [IrOracle.cs](../tests/CathodeRay.Tests/IrOracle.cs) |
| Regresje przeniesione | goto-po-wołaniu (= 39), pętla z wołaniem (= 19) | [RecursionFrameTests.cs](../tests/CathodeRay.Tests/RecursionFrameTests.cs#L107-L151) |
| Macierz celów × IR | `TargetHarness.Targets` × `--ir vreg` | [TargetHarness.cs](../tests/CathodeRay.Tests/TargetHarness.cs#L9-L21) |
| Rozmiar | osobna kolumna/golden dla vreg (nie mieszać z cell) | [TargetSizeTests.cs](../tests/CathodeRay.Tests/TargetSizeTests.cs#L36-L45) |
| Fuzz | `leaf_fuzz`/`recursion_fuzz` karmią oba potoki | `tools/leaf_fuzz.py`, `tools/recursion_fuzz.py` |

Bramka domyślna (`cell`) nie może drgnąć ani o bajt: goldeny `target-sizes.txt` obowiązują
dla `--ir cell` przez cały migration.

## 9. Pliki

Nowe (`src/CathodeRay.C/VReg/`): `VReg.cs`, `VRegLowering.cs`, `VRegFacts.cs`,
`VRegLiveness.cs`, `VRegPasses.cs`, `VRegToCell.cs` (adapter z §4), `VRegInterpreter.cs`,
`Alloc/IVRegAllocator.cs`, `Alloc/AccumulatorAllocator.cs`.
Modyfikowane: [CcCommand.cs](../src/CathodeRay.Cli/CcCommand.cs) (`--ir`),
[ByteIsa.cs](../src/CathodeRay.C/ByteIsa.cs) (opis `VRegTargetInfo`, bez zmian ISA),
`CTargets` (flagi `nes`/`ioPort` — niezależne od VReg, [§D6](#d6-warianty-cpu-przez-rejestr-nie-nową-fabrykę)).
Testy: `VRegLoweringTests`, `VRegLivenessTests`, `VRegInterpreterTests`, `VRegConformanceTests`.

## 10. Fazy (rozwinięcie w planie 37)

1. Flaga `--ir` + szkielet `VReg.cs` (skalary) — kompiluje `return 42`.
2. Lowering wyrażeń i sterowania + `VRegInterpreter` + oracle vs Cell na skalarach.
3. `VRegFacts` + `VRegLiveness` + testy (w tym kopie regresji goto/pętla).
4. `AccumulatorAllocator` + `VRegToCell` + `--ir vreg` end-to-end na stub/6502/Z80.
5. Wołania/ABI przez adapter, `TargetMatrix × vreg` zielona.
6. `LinearScanAllocator`, pomiar vs conservative na celach z rejestrami.
7. SSA/GVN/LICM tylko pod pomiar (osobna decyzja).

## 11. Ryzyka (konkretne, z historią)

- **Soundness Saved**: regresja [646d204](https://github.com/prachwal/CathodeRay/commit/646d204)
  (skok w przód → zły wynik). Mitigacja: reguła §4.4 + przeniesione testy z §8 od dnia 1.
- **Dryf ABI**: adapter (§4) zamiast drugiego emitera; goldeny cell pilnują zamrożenia.
- **6502/ZP**: spill tylko do komórek absolutnych/ZP, nigdy „slotów ramki" (§D2).
- **BCD na NES**: flaga celu gasi ścieżki dziesiętne selektora 6502 (§D6).
- **Dwa IR do utrzymania**: współdzielone testy semantyczne (§8); default = cell do pełnej konformancji.

## Powiązane

- [ir-vreg-plan.md](ir-vreg-plan.md#7-migration-plan) — propozycja ogólna (fazy 0–6).
- [minic-optimization.md](minic-optimization.md#vreg-optimization-pipeline) — potok optymalizacji.
- [stub-calling-conv.md](stub-calling-conv.md#cele-i-kod-pośredni-plan-30-kroki-12) — ABI do reuse.
- Plan pracy: `docs/plans/37-vreg.json` (queued).
