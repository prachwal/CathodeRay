# Testy w CI

Pełna suita trwa 30–45 minut (macierze celów, IR, fuzz), więc nie biegnie na każdym PR. `.github/workflows/ci.yml` używa
`tools/test_gate.py`, tego samego runnera co lokalna bramka.

| Zdarzenie | Co się uruchamia |
|---|---|
| Pull request | `test_gate.py changed --base origin/<gałąź docelowa>`: tylko testy dobrane do zmienionych plików |
| Push na `main` | to samo, względem poprzedniego commita |
| Nocą (03:23 UTC) i `workflow_dispatch` | `test_gate.py full`: build, fuzz, pełna suita shardami, hygiene |
| Nowy push do tego samego PR | przerywa poprzedni, jeszcze trwający przebieg (`concurrency`) |

## Jak wybierane są testy (`select_changes`)

| Zmiana | Testy |
|---|---|
| `docs/**`, `*.md` w katalogu głównym, `.github/**` | żadne (tylko hygiene planów i linków) |
| `samples/compat/**`, `samples/diag/**` | klasa uruchamiająca te próbki |
| Parser, lekser, preprocesor, `TypeChecker*`, `CType*`, `stdlib/**` | testy kompilatora (`C*Tests`), próbki, CLI i strażnicy wygenerowanego kodu (`IrGateTests`, goldeny rozmiarów); bez macierzy backendów |
| `src/CathodeRay.Cli/**` | klasy wołające CLI (`CliApp`/`CcRun`) i strażnicy kodu |
| Asembler, rdzeń, emulatory | cała suita poza wolnymi macierzami, IR i fuzzem |
| Backend jednego CPU (`*Isa.cs`, `*Target.cs`) | testy tego CPU i jego shardy macierzy/IR |
| Wspólny selektor, `Lowering*`, `Legalizer`, alokator, pliki budowania (`*.props`, `global.json`) | bezpieczny fallback: pełny przebieg |

Nieznana ścieżka w `src/` albo `tests/` zawsze oznacza pełny przebieg. Reguły mają testy w `tools/test_gate_test.py`.

Lokalnie: `python3 tools/test_gate.py changed --base main --dry-run` pokazuje, co zostałoby uruchomione.
