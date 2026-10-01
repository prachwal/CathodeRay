---
description: Rozmiar kodu mini-C vs cc65/SDCC (mean/SDCC); baseline przed zmianą
---

`python3 tools/compare.py` wypisuje tabelę funkcji z `samples/bench` i wiersz
„Średnia geometryczna … mini-C / SDCC = X.XX" — **to główna metryka**.

- Przed zmianą codegen zapisz baseline mean/SDCC; po zmianie uruchom ponownie i porównaj (regresja → cofnij lub zawęź zmianę).
- `$ARGUMENTS` = nazwa funkcji (np. `fib`) → wypisz też jej wiersz.
- Wymaga `cc65`/`ca65`/`od65`/`sdcc` w PATH; brak narzędzia → kolumna `n/a`, powiedz o tym.

Nie zmieniaj plików. `docs/compare.md` odświeża dopiero `python3 tools/compare.py --write` — tylko gdy wyraźnie poproszono.
