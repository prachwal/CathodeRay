# Asembler i linker

| Plik | Zawartość | Status |
| --- | --- | --- |
| [z80-assembler.md](z80-assembler.md) | Instrukcja dodania celu Z80 (sloty `{d}`/`{c=N}`, layout, dialekt Zilog) | zrealizowana (cel `z80`/`z80u` istnieje) — do czytania historycznie |
| [assembler-capability-gaps.md](assembler-capability-gaps.md) | Luka wobec ca65 / z80asm / ASXXXX | domknięta: brak otwartych punktów (§2) |
| [linker-segments.md](linker-segments.md) | Analiza: segmenty (wariant A) i pełny linker (wariant B) | zrealizowane oba (plany 09–10): `.segment` + `cathode link` |

Weryfikacja z narzędziami wzorcowymi (ca65+ld65, z80asm, as6800) opisana w [`../technical/assembler-and-linker.md`](../technical/assembler-and-linker.md).
