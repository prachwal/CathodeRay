# Przykłady diagnostyki

Małe programy mini-C do sprawdzania `cc --emit-ir`, `--dump-regalloc`, `--verbose` i `cathode run`.
Każdy plik opisuje w komentarzach `//` oczekiwany wynik; testuje je `tests/CathodeRay.Tests/DiagnosticsSamplesTests.cs`
na z80, 8080, 6502, 6800 i 6809.

| Dyrektywa | Znaczenie |
|---|---|
| `// expect-result: N` | wartość zwrócona przez `main` (ze znakiem, 16 bitów) odczytana przez `cathode run -l` |
| `// expect-result-text: …` | dokładna linia z wyjścia `run` |
| `// expect-exit: N` | kod wyjścia `run` (np. 2 = limit kroków) |
| `// expect-stderr: …` | fragment stderr z `run` |
| `// expect-warning: …` | fragment ostrzeżenia kompilatora (stderr `cc`) |
| `// expect-verbose: …` | fragment komunikatu `cc -v` |
| `// expect-ir: …` / `// expect-ir-absent: …` | tekst obecny / nieobecny w zrzucie `--emit-ir` |
| `// expect-regalloc: …` | tekst w zrzucie `--dump-regalloc` (sprawdzany tylko na z80 i 8080) |

Ręcznie:

```bash
cathode cc samples/diag/d01_sum_loop.c -o /tmp/d.bin -l /tmp/d.lst --cpu z80 -v --emit-ir - --dump-regalloc -
cathode run /tmp/d.bin --cpu z80 -l /tmp/d.lst --trace --trace-limit 20
```
