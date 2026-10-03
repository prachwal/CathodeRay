# Asembler Z80: instrukcja implementacji

Dokument dla osoby (lub agenta), która doda cel `--cpu z80` do asemblera w `src/CathodeRay.Assembler`.
Opisuje, co z obecnej architektury wystarcza bez zmian, czego brakuje w rdzeniu i w jakiej kolejności to zrobić.
Stan wyjściowy: cele `6502`, `6502x`, `65c02`, `8080`, `stub`; testy wzorcowe z ca65 i z80asm zielone.

## 1. Jak działa obecny asembler (w skrócie)

Trzy wymienne osie, rdzeń ich nie zna:

| Oś | Gdzie | Co robi |
| --- | --- | --- |
| Cel (CPU) | `Isa/Targets/*Set.cs` + wpis w `AssemblerTargets.cs` | Adapter danych ISA JSON → `InstructionSet` (lista `InstructionForm`: mnemonik + `OperandPattern` + bajty opcode'u) |
| Dialekt | `Syntax/Dialects/*.cs` (instancje `SyntaxDialect`) | Zapis liczb, symbol PC, etykiety, operatory, nazwy dyrektyw |
| Rdzeń | `TwoPassAssembler`, `FormSelector`, `OperandPattern`, `Expression`, `LineParser`, `Directives/*` | Dwa przebiegi, wybór formy, emisja, listing |

Mechanizmy rdzenia, na których Z80 się opiera:

- **Wzorce operandów**: literały + sloty `{b}` (bajt 0..255), `{w}` (słowo), `{r}` (skok względny od końca instrukcji).
  Slot nie obejmuje przecinka spoza apostrofów.
- **Wybór formy** (`FormSelector`): najwięcej literałów wygrywa, potem najkrótsza forma z mieszczącymi się wartościami,
  odwołanie w przód → najdłuższa. Jeśli operand pasuje do bardziej szczegółowego kształtu z całego zestawu, którego ta
  instrukcja nie ma, to błąd „addressing mode X is not available” (tak działa `LDA ($12)` na NMOS 6502).
- **Opcode jako `byte[]`**: prefiksy `CB`/`DD`/`ED`/`FD` mieszczą się w modelu od początku.
- **Kolejność bajtów słów** z `InstructionSet.Endianness` (Z80: little-endian).

Dla Z80 to wystarcza w około 90%. Braki opisuje punkt 3.

## 2. Dane: `data/instructions/mcp_z80_instructions.json`

Pliku jeszcze nie ma. Format jak w `mcp_8080_instructions.json`, z polem `mnemonic` jako **szablonem składni Zilog**:

```json
{ "opcode": "DD7E", "mnemonic": "LD A,(IX+d)", "cycles": 19, "words": 3, "group": "load", "encoding": "DD 7E d", "operands": [...] }
{ "opcode": "DDCB06", "mnemonic": "RLC (IX+d)", "cycles": 23, "words": 4, "encoding": "DD CB d 06", ... }
{ "opcode": "18", "mnemonic": "JR e", "words": 2, ... }
{ "opcode": "CB46", "mnemonic": "BIT 0,(HL)", "words": 2, ... }
```

Placeholdery w szablonach (moduł zamienia je na sloty):

| Placeholder | Znaczenie | Slot |
| --- | --- | --- |
| `n` | bajt natychmiastowy | `{b}` |
| `nn` | słowo (wartość albo adres) | `{w}` |
| `e` | skok względny JR/DJNZ | `{r}` |
| `d` | przesunięcie ze znakiem w `(IX+d)`/`(IY+d)` | **nowy** `{d}` (punkt 3.1) |

Numer bitu w `BIT/SET/RES`, tryb `IM 0/1/2` i adres `RST 00H..38H` są częścią opcode'u. Zapisz je jako osobne wpisy
(`BIT 3,A` → `CB5F`) i zamień na slot stałej (punkt 3.2), a nie na literał. Bez tego `BIT 1+2,A` albo `RST 56` nie zadziała.

Zakres danych:

- udokumentowane: 252 opcode'y bazowe (bez prefiksów), `CB` (256), `ED` (~58), `DD`/`FD` (warianty IX/IY instrukcji z HL),
  `DDCB`/`FDCB` (bity i przesunięcia na `(IX+d)`);
- nieudokumentowane (osobny wariant, jak `6502x`): `IXH/IXL/IYH/IYL`, `SLL`, `OUT (C),0`, `IN (C)` / `IN F,(C)`.

Źródła: Zilog UM0080 (Z80 CPU User Manual) oraz tabele dekodowania z z80.info. **Nie wpisuj danych ręcznie bez weryfikacji**:
najpierw wygeneruj wzorzec z `z80asm` (punkt 6) i porównaj. Przy 6502 dane zawierały 11 błędnych trybów adresowania,
które wyszły dopiero w porównaniu z ca65.

## 3. Zmiany w rdzeniu (małe, ogólne, przydadzą się też innym CPU)

### 3.1 Slot przesunięcia ze znakiem `{d}`

- `Isa/FieldKind.cs`: dodaj `Displacement8` (−128..127, emitowany jako bajt w kodzie U2).
- `Isa/OperandPattern.cs`: w `Parse` mapuj `"d" => FieldKind.Displacement8`.
- `Isa/FormSelector.cs` → `Fits`: `Displacement8 => values[i] is >= -128 and <= 127`.
- `TwoPassAssembler.EmitField`: gałąź `Displacement8` z komunikatem „displacement out of range -128..127”.

Wzorzec IX zapisz jako `(IX{d})`, a nie `(IX+{d})`: slot złapie wtedy `+5` albo `-5`, a parser wyrażeń obsłuży znak.
`(IX)` bez przesunięcia to osobna forma z wbudowanym bajtem 0 (punkt 3.3).

### 3.2 Slot stałej `{c=N}`

Dla operandów, które wybierają opcode zamiast trafiać do kodu (`BIT 3,…`, `IM 1`, `RST 38H`):

- `FieldKind.Constant` + wartość w `OperandPattern` (np. osobna lista `Constants` równoległa do `Fields`).
- Dopasowanie: slot łapie wyrażenie jak każdy inny.
- `Fits`: wartość **musi być znana** i równa N. Nieznana w pierwszym przebiegu → błąd „must be known”, bo od niej zależy
  opcode, a więc rozmiar.
- Emisja: zero bajtów.
- `InstructionForm.Size` nie liczy tego slotu.

Po tej zmianie warto przepiąć `RST 0..7` w `Intel8080Set` z literałów na `{c=N}` (dziś `RST 3+4` nie działa).

### 3.3 Kolejność bajtów w instrukcji (layout)

`DDCB`/`FDCB` mają przesunięcie **przed** ostatnim bajtem opcode'u: `DD CB d 06`. Obecnie `TwoPassAssembler.Instruction`
emituje najpierw cały opcode, potem pola. Rozszerzenie:

- `InstructionForm` dostaje opcjonalny `Layout`: lista elementów `Literal(byte)` / `Field(index)`.
  Domyślnie (gdy `null`): wszystkie bajty opcode'u, potem pola po kolei, czyli zachowanie jak dziś.
  `Size` liczony z layoutu.
- `TwoPassAssembler.Instruction`: emisja według layoutu. Wartości pól liczone **przed** emisją, bo `*`/`$` ma wskazywać
  początek instrukcji.
- `Z80Set`: dla opcode'u zaczynającego się od `DDCB`/`FDCB` → layout `[p0, p1, Field(0), p2]`;
  dla `(IX)`/`(IY)` bez przesunięcia → `[DD, op, Literal(0)]` itd.

Skoki względne liczone są od `start + form.Size`, więc działają bez zmian (JR/DJNZ mają 2 bajty).

### 3.4 Apostrof w `AF'`

`EX AF,AF'` psuje dziś dwie rzeczy: `LineParser.StripComment` i `OperandList.Split` traktują `'` jako początek łańcucha,
więc komentarz po `AF'` nie zostanie usunięty. Reguła do dodania: apostrof **bezpośrednio po znaku identyfikatora**
(`AF'`) nie otwiera łańcucha. Dodaj test: `EX AF,AF' ; komentarz` → `08`.

## 4. Moduł `Isa/Targets/Z80Set.cs`

Wzorzec: `Intel8080Set` (szablon z pola `mnemonic`), z rozszerzeniami:

1. Podział szablonu na mnemonik i operand, zamiana placeholderów: `nn` → `{w}`, `n` → `{b}`, `e` → `{r}`, `d` → `{d}`
   (regex z granicą słowa; najpierw `nn`, potem `n`). Uwaga: `(IX+d)` → `(IX{d})`.
2. Stałe: `BIT b,…`, `SET b,…`, `RES b,…`, `IM m`, `RST p` → `{c=N}` z wartością z szablonu.
3. Layout dla `DDCB`/`FDCB` i form `(IX)`/`(IY)` bez przesunięcia (punkt 3.3).
4. Warianty jak `Mos6502Variant`: `Z80Variant.Documented` / `Undocumented` (flaga CLI `--cpu z80` / `z80u`).
5. Duplikaty (np. nieudokumentowane `ED`-aliasy `NEG`, `RETN`, `IM`): kolejność form rosnąco po opcode, pierwszy wygrywa,
   jak w `Intel8080Set`.
6. Kontrola danych w module: rozmiar formy (z layoutu) == `words` z JSON, inaczej `InvalidDataException`.

Dlaczego wzorce z nawiasami działają bez specjalnego kodu: `LD A,(1234H)` pasuje do `A,({w})` (4 literały) i do `A,{b}`
(2 literały). Wygrywa bardziej szczegółowy, czyli odczyt z pamięci, jak u Zilogu. `LD A,(2+3)*4` nie pasuje do `A,({w})`
(nawias nie zamyka operandu), więc to wartość natychmiastowa. Rejestry (`LD A,B`) mają 3 literały i wygrywają z `A,{b}`.

## 5. Dialekt `Syntax/Dialects/Zilog.cs`

```csharp
public static SyntaxDialect Zilog { get; } = new()
{
    Name = "zilog",
    Numbers = NumberFormats.Intel | NumberFormats.Motorola | NumberFormats.CStyle, // 0FFH, $FF, 0xFF, %1010
    ProgramCounter = '$',          // '$' + cyfra hex = liczba, samo '$' = PC (Expression już tak rozróżnia)
    WordOperators = false,          // do rozważenia: z80asm ma LOW/HIGH jako funkcje
    AssignmentKeywords = { "EQU", "DEFL", "=" },
    Directives = DirectiveTable(
        ("ORG", Org), ("DB", Byte), ("DEFB", Byte), ("DEFM", Byte), ("DW", Word), ("DEFW", Word),
        ("DS", Reserve), ("DEFS", Reserve), ("END", End)),
};
```

Sprawdź w `Expression.Primary`, że `%1010` (Motorola) nie koliduje z `%` jako modulo: prefiks jest rozpoznawany tylko
w pozycji operandu, więc powinno działać, ale dodaj test.

## 6. Weryfikacja z oryginalnym narzędziem

`z80asm` z z88dk jest zainstalowany: `/opt/z88dk-2.4/bin/z80asm`, wymaga `ZCCCFG=/opt/z88dk-2.4/lib/config`.
Składnia Zilog to jego natywna składnia, więc jest dobrym wzorcem (lepszym niż dla 8080).

1. W `tools/make_asm_golden.py` dodaj `golden_z80()` na wzór `golden_8080()`: linia na każdy szablon z JSON
   z przykładowymi wartościami (`n` → `12h`, `nn` → `1234h`, `d` → `+5`, `e` → `$+5`), asemblacja
   `z80asm -mz80 -b` (wariant nieudokumentowany: `-mz80` zamiast `-mz80_strict`, sprawdź w `z80asm -h`).
2. Linie, które `z80asm` odrzuca, zapisz do `Asm/z80/rejected.txt`, jak dla ca65. Nasz asembler musi je też odrzucić.
3. `AsmGoldenTests` obsłuży katalog `Asm/z80` automatycznie (katalog = nazwa `--cpu`, dialekt domyślny celu).
4. Znane różnice, które trzeba wykluczyć z porównania i pokryć testem ręcznym (jak `RST`/`JP`/`CP` przy 8080):
   sprawdź zapis `RST` (z80asm przyjmuje adres `38h`; to zgodne z Zilogiem) i ewentualne aliasy mnemoników.
5. Testy ręczne (plik `tests/CathodeRay.Tests/Z80AsmTests.cs`):
   - `LD A,(IX+5)` → `DD 7E 05`, `LD A,(IX-1)` → `DD 7E FF`, `LD A,(IX)` → `DD 7E 00`;
   - `RLC (IX+2)` → `DD CB 02 06` (layout);
   - `BIT 7,(HL)` → `CB 7E`, `BIT 3+4,(HL)` → to samo (slot stałej), `BIT 8,(HL)` → błąd;
   - `JR $` → `18 FE`, `DJNZ` poza zakresem → błąd;
   - `EX AF,AF' ; komentarz` → `08`;
   - `LD A,(1234H)` → `3A 34 12`, `LD A,(2+3)*4` → `3E 14`;
   - `IM 2` → `ED 5E`, `RST 38H` → `FF`, `RST 56` → `FF`.

## 7. Rejestracja i CLI

- `AssemblerTargets.All`: `new("z80", "Zilog Z80", "mcp_z80_instructions.json", json => Z80Set.Load(json, Z80Variant.Documented), [SyntaxDialects.Zilog])`
  i analogicznie `z80u`.
- `src/CathodeRay.Cli/CathodeRay.Cli.csproj`: dopisz `mcp_z80_instructions.json` do elementu `<None Include=...>`.
- `AsmCommand` nie wymaga zmian (lista CPU i dialektów bierze się z rejestru).
- Opcjonalnie: dialekt `intel` dla Z80 nie ma sensu (mnemoniki Intela nie opisują rozszerzeń Z80), więc tylko `zilog`.

## 8. Kolejność prac i kryterium ukończenia

1. Rdzeń: `{d}`, `{c=N}`, layout, `AF'` + testy jednostkowe każdego mechanizmu (bez Z80).
2. Dane JSON Z80 (skrypt w `tools/`, nie ręcznie), walidacja liczby opcode'ów dla każdej grupy prefiksów.
3. `Z80Set` + dialekt `Zilog` + rejestracja.
4. `golden_z80()` + `Z80AsmTests`.
5. `dotnet build -warnaserror` i `dotnet test` zielone; `cathode asm prog.asm --cpu z80 -l prog.lst` działa.

Czego **nie** robić: nie dodawaj do rdzenia wiedzy o Z80 (np. `if (mnemonic == "BIT")`). Wszystko, co specyficzne dla
CPU, należy do `Z80Set`; rdzeń dostaje tylko ogólne mechanizmy (slot przesunięcia, slot stałej, layout).

Emulator Z80 (rdzeń CPU w `src/CathodeRay`) to osobne zadanie; asembler od niego nie zależy.
