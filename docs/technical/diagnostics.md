# Diagnostyka kompilatora i uruchamiania

Narzędzia do szukania błędów bez zmiany kodu kompilatora. Żadne z nich nie zmienia generowanego obrazu
(`Diagnostics_Do_Not_Change_The_Image`).

## `cathode cc`

| Opcja | Działanie |
|---|---|
| `-v`, `--verbose` | na stderr: cel, IR, ABI, optymalizacja; które moduły biblioteki standardowej zostały dołączone i dlaczego; rozmiar obrazu |
| `--emit-ir <plik>` | kod pośredni modułów użytkownika po etapie `lowered` (po obniżeniu i inlinowaniu), a przy `--ir vreg` także po `vreg`; `-` = stderr |
| `--dump-regalloc <plik>` | przydział rejestrów alokatora (z80, 8080): komórka → rejestr/para albo `memory` z przyczyną (`multi`, `addrof`, `wide`, `no-color`, …); `-` = stderr |
| `--stats`, `-l`, `--map`, `-Werror`, `-O0`, `--ir`, `--abi` | rozmiary segmentów, listing, mapa C:linia, ostrzeżenia jako błędy, wyłączenie optymalizatora, ścieżki IR/ABI do porównań |

Zrzuty obejmują tylko moduły użytkownika, nie bibliotekę standardową. Wielokrotne użycie tej samej ścieżki w
`--emit-ir` i `--dump-regalloc` zapisuje do jednego pliku.

Format IR: `func nazwa(parametry) ret=BB`, potem `numer: instrukcja`; komórka to `symbol:szerokość`, stała `#wartość:szerokość`,
adres `&symbol`. Znaczniki `;c: plik:linia` wiążą instrukcje ze źródłem C.

## `cathode run`

```bash
cathode cc prog.c -o prog.bin -l prog.lst --cpu z80
cathode run prog.bin --cpu z80 -l prog.lst            # wynik main i konsola
cathode run prog.bin --cpu z80 --trace --trace-limit 50
cathode run prog.bin --cpu z80 --dump 0x7000:32       # zrzut pamięci po zakończeniu
```

Emulatory: z80, 8080, 6502/65c02/nes/6510, 6800, 6809 (biblioteka `CathodeRay.Emulation`, wspólna z testami).
Stub ma osobną komendę `cathode stub run`.

- `--load` / `--start`: domyślnie początek pamięci układu celu i początek obszaru kodu (crt0); nadpisz, gdy używasz własnej konfiguracji (`--config`).
- `--trace`: linia na krok — PC sprzed kroku, trzy bajty spod PC, rejestry po kroku. `--trace-limit` (domyślnie 10000) chroni przed zalaniem.
- `-l`: z listingu odczytuje `cc_ret` (wynik `main`) oraz bufor konsoli `__io_buf`.
- Kod wyjścia: 0 zatrzymany, 2 limit kroków (`--max-steps`), 1 błąd (np. nieobsługiwany opkod).

## Kod: `ICompilerTrace`

Biblioteka `CathodeRay.C` udostępnia `CompilerTrace.Begin(ICompilerTrace)`; bez odbiornika kompilator nie liczy ani nie wypisuje niczego.
Hooki: `Codegen.Lower` (`lowered`), `VRegPipeline.Lower` (`vreg`), `RegisterAllocator.Tune` (przydział). Nowy etap = jedno wywołanie
`CompilerTrace.Current?.Ir("nazwa", module)`. Zamiast tymczasowych `Console.Error.WriteLine` w kodzie używaj tego mechanizmu.

## Ograniczenia

- `run --cpu 65c02` używa emulatora NMOS 6502: rozkazy tylko-CMOS (`STZ`, `BRA`, `PHX`, …) kończą się komunikatem o nieobsługiwanym rozkazie.
- Konsola (`__io_buf`) ma 256 bajtów z 8-bitowym kursorem; po zawinięciu `run -l` pokazuje tylko ostatnie `__io_cur` bajtów bufora.
- Znaki sterujące w nazwach modułów i wyjściu konsoli są zastępowane, żeby niezaufany program nie sterował terminalem.
- Ścieżki `--emit-ir` i `--dump-regalloc` nie mogą wskazywać plików wejściowych ani wyjść `-o`, `-l`, `--map`, `--config`.
