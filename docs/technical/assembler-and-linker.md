# Asembler i linker

Źródła: [../z80-assembler.md](../z80-assembler.md), [../assembler-capability-gaps.md](../assembler-capability-gaps.md),
[../linker-segments.md](../linker-segments.md). Kod: `src/CathodeRay.Assembler`.

## Obsługiwane procesory i dialekty

6502, 6502x, 65C02, Intel 8080, Z80 (z wariantem z nieudokumentowanymi instrukcjami), Motorola 6800 i `stub`.
Dialekty składni (Zilog, Intel, MOS w stylu ca65, Motorola) są danymi (`SyntaxDialect`), a nie osobnym kodem.

## Architektura

Rdzeń składa się z trzech wymiennych osi, które nie znają się nawzajem:

| Oś | Gdzie | Rola |
| --- | --- | --- |
| Cel (CPU) | `Isa/Targets/*Set.cs`, `AssemblerTargets.cs` | adapter danych ISA (JSON) do listy form instrukcji: mnemonik, wzorzec operandów, bajty opcode'u |
| Dialekt | `Syntax/Dialects/*.cs` | zapis liczb, symbol PC, etykiety, operatory, nazwy dyrektyw |
| Rdzeń | `TwoPassAssembler`, `FormSelector`, `OperandPattern`, `Expression`, `LineParser`, `Directives/*` | dwa przebiegi, wybór formy, emisja, listing |

Nowy CPU to w praktyce plik JSON z opisem instrukcji, moduł w `Isa/Targets` i wpis w `AssemblerTargets`.

### Wybór formy instrukcji

Wzorce operandów mają literały i sloty: `{b}` (bajt), `{w}` (słowo), `{r}` (skok względny).
Wygrywa forma z największą liczbą literałów, potem najkrótsza, w której mieszczą się wartości.
Odwołanie w przód wybiera formę najdłuższą (jak w oryginalnych asemblerach). Jeśli operand pasuje do trybu, którego dana instrukcja nie ma,
asembler zgłasza błąd „addressing mode X is not available” zamiast po cichu go reinterpretować.
Opcode to `byte[]`, więc prefiksy Z80 (`CB`, `DD`, `ED`, `FD`) mieszczą się w modelu.

## Funkcje

- Makra (`.macro`/`MACRO`, parametry z wartościami domyślnymi, `.LOCAL`), asemblacja warunkowa (`.if/.elseif/.else/.endif`, operatory relacyjne),
  `.include` z `--incdir` i wykrywaniem cykli, `.incbin`, `.align N[,fill]`.
- Symbole: tanie etykiety lokalne `@`, zakresy `.scope`/`.proc` z dostępem `a::b::c`.
- Dyrektywy ca65 typu long-tail: `.define`, `.assert`, `.out`, `.warning`, `.error`, `.ifblank`, `.paramcount`.
- Listing (`-l`), pełna lista błędów (limit 20, format `plik: line N`), wyjście `.bin`, Intel HEX (`-f hex`) i obiekt (`-f obj`).

## Segmenty i linker

Asembler wspiera segmenty (`.segment`, `.code`, `.data`, `.bss`) z osobnym licznikiem na każdy. BSS nic nie emituje.
Adresy segmentów podaje się flagą `--map NAZWA@adres`.

Pełny linker modułów (wariant B z `linker-segments.md`):

- format obiektu `cathode-obj/1` (`-f obj`) z symbolami `GLOBAL`/`EXTERN`,
- relokacje Abs8, Abs16, Disp8, Rel8,
- `cathode link` z plikiem `.cfg` w stylu `ld65` (sekcje `MEMORY` i `SEGMENTS`),
- standardowe segmenty `CODE`, `DATA`, `BSS`, `INIT`, a dla symboli końca segmentu linker dodaje `__<segment>_end`.

Decyzja projektowa, która doprowadziła do tego kształtu: najpierw wariant A (segmenty w jednym wywołaniu, tani),
a pełny linker dopiero po tym, jak stał się potrzebny. Kolejność zadań jest w planach 07, 09 i 10.

## Weryfikacja względem narzędzi wzorcowych

Wyniki asemblacji są porównywane bajt w bajt z oryginalnymi narzędziami: ca65 + ld65 (6502), z80asm (Z80 i 8080) i as6800 (6800).
Wzorce (goldeny) generują skrypty `tools/make_*_golden.py` (m.in. `make_asm_golden.py`, `make_macros_golden.py`, `make_segments_golden.py`,
`make_modules_golden.py`), a odrzucane wejścia zapisuje plik `rejected.txt`.
Przy wprowadzaniu danych ISA obowiązuje zasada: nie wpisywać ich ręcznie bez porównania z wzorcem.
Przy 6502 dane zawierały 11 błędnych trybów adresowania, które wyszły dopiero w porównaniu z ca65.

## Co dalej (z dokumentu luk)

Lista braków względem ca65 / z80asm / ASXXXX jest pusta. Kandydaci na kolejne tematy: listing z gałęziami warunkowymi i format S-record dla 6800.
