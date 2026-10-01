# CathodeRay — zasady pracy

Asembler multi-CPU + kompilator mini-C (C → IR → legalizer → selektor bajtowy). Zasady ogólne są w globalnym `AGENTS.md`; tu tylko realia tego repo.

## Weryfikacja (kolejność)
- Build: `dotnet build -warnaserror` (StyleCop/Roslynator to błędy, nie ostrzeżenia).
- W trakcie pracy testy **celowane**: `dotnet test tests/CathodeRay.Tests/CathodeRay.Tests.csproj --filter "<Klasa>"`. Pełna suita (~8 min) dopiero przed commitem.
- Komendy pomocnicze: `/verify <filtr>`, `/bench [funkcja]`, `/golden`, `/fuzz`.
- Zmiana codegen → `/fuzz` (różnicowo vs gcc) i `/bench` (mean/SDCC to główna metryka).
- Zmiana rozmiarów → `/golden` (odśwież goldeny i sprawdź brak wzrostów).

## Ręczne inspekcje (CLI)
`dotnet run --project src/CathodeRay.Cli/CathodeRay.Cli.csproj -- cc <plik.c> --cpu <cel> --listing /tmp/x.lst`,
opcjonalnie `--ir vreg`, `--abi v1|v2`, `--stats`, `--incdir samples/minic`. Listingu użyj, żeby **zobaczyć** kod przed/po, nie zgadywać.
Cele: `stub 6502 65c02 nes 6510 z80 8080 6800`.

## Zanim wyprowadzisz semantykę — przeczytaj źródło
Flagi, szerokości operandów, carry/overflow, strony zerowe, ABI: `src/CathodeRay.C/CpuModels.cs` (model rejestrowy),
emulator w `tests/CathodeRay.Tests/` (`Mos6502.cs`, `Z80Cpu.cs`, …) i `docs/`.
**Nigdy nie zgaduj z pamięci** — model flag (kto gasi C, co ustawia V) był źródłem błędnych skrótów przy optymalizacjach.

## Goldeny rozmiarów
- `tests/CathodeRay.Tests/target-sizes.txt` (`UPDATE_TARGET_SIZES=1 …`) i `vreg-sizes.txt` (`UPDATE_VREG_SIZES=1 …`);
  render w `docs/target-sizes.md` i `docs/vreg-sizes.md`.
- **Nie mogą rosnąć bez świadomej decyzji**; każdy wzrost uzasadniaj w nocie planu.
- Zmiany w selektorze/legalizerze drgają także na 6502/6800 (wspólny selektor) — sprawdzaj wszystkie cele, nie tylko Z80.

## Plany
- Tylko przez MCP `plan` albo `python3 tools/plan.py …` (`docs/plans/*.json` to źródło, `.md` to render).
- `python3 tools/plan.py hygiene` musi być OK przed commitem. Jeden plan w locie.

## Dokumentacja
`docs/minic.md` (język mini-C), `docs/targets.md` (cele/formaty), `docs/compare.md` (rozmiary vs cc65/SDCC),
`docs/ir-vreg-plan.md` + `docs/vreg-design.md` (VReg), `docs/stub-calling-conv.md` (ABI).

## Styl kodu
- Jeden typ na plik (SA1402); kolejność członków — statyczne/prywatne wg SA1204 (statyczne przed instancyjnymi).
- `<summary>/<param>` po polsku, regex tylko jako `[GeneratedRegex]` w `partial`, `Environment.NewLine`, w regex `\r?\n`, mnemoniki małymi literami.
- Diff minimalny; nie ruszaj niepowiązanych plików.

## Gałęzie i commity
- Pracuj na gałęzi serii (`opt/…`, `vreg/…`); konfigurację i dokumentację trzymaj z dala od zmian codegen.
- Commit/PR tylko na wyraźne polecenie.
- Bramki przed commitem: `build -warnaserror`, pełna suita zielona, goldeny bez wzrostu, fuzz zielony.
