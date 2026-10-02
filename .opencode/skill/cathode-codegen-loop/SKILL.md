---
name: cathode-codegen-loop
description: Use when optimizing or changing CathodeRay codegen (selector, legalizer, ISA, IR passes) or executing a plan batch from docs/plans (e.g. plan 39/40). Enforces the measure → implement → targeted test → golden no-growth → fuzz loop and the repo's gate order. Trigger on "optymalizuj", "zrób batch", "codegen", "strength reduction", "LDIR/DJNZ/RST", "goldeny", "mean/SDCC".
---

# Pętla zmian codegen (CathodeRay)

Wymuszaj tę kolejność. Nie commituj bez pełnego zestawu bramek.

## 0. Rozpoznanie (przed kodem)
- Przeczytaj źródło, **nie zgaduj**: `src/CathodeRay.C/CpuModels.cs` (flagi/rejestry/scratch), emulator w `tests/CathodeRay.Tests/` (`Mos6502.cs`, `Z80Cpu.cs`, …), `docs/`. Model flag bywał źródłem błędnych skrótów.
- Ustal CPU, na których zmiana działa, i te, które **muszą zostać bit-identyczne** (wspólny selektor → 6502/6800 też drgają).
- Przed implementacją zmierz baseline: `/bench`. Dla zmiany rozmiarów zapamiętaj interesujące wiersze.

## 1. Implementacja
- Diff minimalny, jeden typ na plik, kolejność członków wg SA1204, `<summary>` po polsku.
- Jeśli optymalizacja zależy od własności CPU, dodaj ją jako wirtualną właściwość `ByteIsa` (domyślnie `false`) zamiast `if (cpu == ...)`.

## 2. Test celowany (na bieżąco, nie pełna suita)
- `/verify "FullyQualifiedName~<Klasa>"`.
- Nową logikę pokryj **jednym** testem z prawdziwym oracle (emulator/interpreter/gcc), nie samymi asercjami na listingu.

## 3. Bramki przed commitem
- `/bench` — porównaj mean/SDCC z baseline; przy regresji **cofnij lub przenieś do węższego haka**.
- `/golden` — **zero wzrostów** na wszystkich celach; każdy wzrost uzasadnij w nocie planu.
- `/fuzz` — 60 leaf + 60 recursion zielone.
- Pełna suita: `dotnet test tests/CathodeRay.Tests/CathodeRay.Tests.csproj`.

## 4. Plan i commit
- Status zadania przez MCP `plan`/`tools/plan.py`; `python3 tools/plan.py hygiene` OK.
- Commit tylko na wyraźne polecenie; komunikat: `feat(<cel>): plan <N> <zakres>`, w treści **liczby** (przed/po) i powód odrzucenia wariantów.

## Zasady twarde
- „Odrzucone” jest wynikiem: SDCC-rotacja (wash 18=18 B), 8080-fold (każdy ALU gasi borrow), RST (zysk w wierszach spoza średniej) — zapisuj to w nocie, nie przepychaj.
- Gdy ryzyko > zysk, odrocz **z uzasadnieniem w nocie planu**, nie wymuszaj.
- Zachowaj zmiany użytkownika; nie commituj plików spoza zakresu.
