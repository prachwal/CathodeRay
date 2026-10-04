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
Cele: `stub 6502 65c02 nes 6510 z80 8080 6800 6809`.

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
- `python3 tools/plan.py hygiene` musi być OK przed commitem (sprawdza też martwe linki w `*.md` przez `tools/check_links.py`). Jeden plan w locie.

## Dokumentacja

`docs/minic.md` (język mini-C), `docs/codegen/targets.md` (cele/formaty), `docs/compare.md` (rozmiary vs cc65/SDCC),
`docs/ir-vreg-abi/ir-vreg-plan.md` + `docs/ir-vreg-abi/vreg-design.md` (VReg), `docs/codegen/stub-calling-conv.md` (ABI).

- Aktualizacja dokumentacji jest bramką przed commitem dla zmian codegen/CPU: odśwież `docs/codegen/opt-<cpu>.md` i checklistę `docs/technical/mini-c-status.md`.
- Zmiana lub dodanie celu/ABI wymaga też aktualizacji `docs/codegen/targets.md` i indeksu `docs/codegen/README.md`.
- Zmiana rozmiarów wymaga odświeżenia `docs/target-sizes.md` i/lub `docs/vreg-sizes.md` zgodnie z testami goldenów; metryki i odrzucone warianty zapisuj w planie.
- Renderuj zmieniony plan przez `python3 tools/plan.py render <plan>` i przed commitem wymagaj `python3 tools/plan.py hygiene`.

## Styl kodu

- Jeden typ na plik (SA1402); kolejność członków — statyczne/prywatne wg SA1204 (statyczne przed instancyjnymi).
- `<summary>/<param>` po polsku, regex tylko jako `[GeneratedRegex]` w `partial`, `Environment.NewLine`, w regex `\r?\n`, mnemoniki małymi literami.
- Diff minimalny; nie ruszaj niepowiązanych plików.

## Gałęzie i commity

- Pracuj na gałęzi serii (`opt/…`, `vreg/…`); konfigurację i dokumentację trzymaj z dala od zmian codegen.
- Commit/PR tylko na wyraźne polecenie.
- Bramki przed commitem: aktualna dokumentacja (albo uzasadnienie braku zmian w planie), `build -warnaserror`, pełna suita zielona, goldeny bez wzrostu, fuzz zielony.

<!-- gitnexus:start -->
# GitNexus — Code Intelligence

This project is indexed by GitNexus as **CathodeRay** (6978 symbols, 19016 relationships, 429 execution flows).

> Index stale? Run `node .gitnexus/run.cjs analyze --index-only` from the project root — it auto-selects an available runner. No `.gitnexus/run.cjs` yet? Bootstrap with `npx`, `bunx`, or `pnpm dlx` — e.g. `bunx gitnexus@latest analyze` (npm 11 npx crash; #1939).

## Always Do

- **MUST run impact analysis before editing.** Use `impact({target: "symbolName", direction: "upstream"})` (MCP) or `node .gitnexus/run.cjs impact "symbolName" --direction upstream --repo .` (CLI fallback); report callers, processes, and risk. Never substitute grep for graph analysis.
- **MUST analyze graph changes before committing.** Use `detect_changes({scope: "all"})` (MCP) or `node .gitnexus/run.cjs detect-changes --scope all --repo .` (CLI fallback). `partial: true` or `truncated: true` is not a clean check — a zero means unseen, not unaffected; re-run it. For regression review: `detect_changes({scope: "compare", base_ref: "main"})` or `node .gitnexus/run.cjs detect-changes --scope compare --base-ref "main" --repo .`.
- **MUST warn the user** if impact analysis returns HIGH or CRITICAL risk before proceeding with edits.
- **MUST treat `risk: UNKNOWN` as unresolved, not as low.** An empty caller set is not evidence the symbol is unused — it can also mean the callers are not resolvable by the index (plain-object property access, dynamic dispatch, cross-language calls). `impact` pairs `UNKNOWN` with a `riskNote` saying so. Confirm with a text search before treating the symbol as safe to change or delete; do not proceed on the strength of a zero.
- When exploring unfamiliar code, use `query({search_query: "concept"})` to find execution flows instead of grepping. It returns process-grouped results ranked by relevance.
- When you need full context on a specific symbol — callers, callees, which execution flows it participates in — use `context({name: "symbolName"})`.
- For security review, `explain({target: "fileOrSymbol"})` lists taint findings (source→sink flows; needs `analyze --pdg`).

## Never Do

- NEVER edit a function, class, or method before MCP/CLI impact analysis.
- NEVER ignore HIGH or CRITICAL risk warnings from impact analysis, and never read `UNKNOWN` as an all-clear — it means the walk could not answer, which is the one verdict that requires confirming by other means.
- NEVER rename symbols with find-and-replace — use `rename` which understands the call graph.
- NEVER commit before MCP/CLI graph change analysis.

## Resources

| Resource | Use for |
| --- | --- |
| `gitnexus://repo/CathodeRay/context` | Codebase overview, check index freshness |
| `gitnexus://repo/CathodeRay/clusters` | All functional areas |
| `gitnexus://repo/CathodeRay/processes` | All execution flows |
| `gitnexus://repo/CathodeRay/process/{name}` | Step-by-step execution trace |

## CLI

| Task | Read this skill file |
| --- | --- |
| Understand architecture / "How does X work?" | `.claude/skills/gitnexus-exploring/SKILL.md` |
| Blast radius / "What breaks if I change X?" | `.claude/skills/gitnexus-impact-analysis/SKILL.md` |
| Trace bugs / "Why is X failing?" | `.claude/skills/gitnexus-debugging/SKILL.md` |
| Rename / extract / split / refactor | `.claude/skills/gitnexus-refactoring/SKILL.md` |
| Tools, resources, schema reference | `.claude/skills/gitnexus-guide/SKILL.md` |
| Index, status, clean, wiki CLI commands | `.claude/skills/gitnexus-cli/SKILL.md` |

<!-- gitnexus:end -->
