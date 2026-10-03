# IR (Cell/VReg) i ABI v2

| Plik | Zawartość | Status |
| --- | --- | --- |
| [ir-vreg-plan.md](ir-vreg-plan.md) | Pierwotna propozycja VReg IR (dwa IR, fabryka, fazy 0–6) | zastąpiony przez `vreg-design.md`; fazy zrealizowane w planie 37 |
| [vreg-design.md](vreg-design.md) | Projekt techniczny VReg: adapter do Cell IR, spill komórkowy, alokatory | zaimplementowany (plan 37, closed); `--ir vreg` działa, default to `cell` |
| [vreg-alloc.md](vreg-alloc.md) | Pomiar alokatorów VReg (trzymane vs spillowane) | generowany: `UPDATE_VREG_ALLOC=1 dotnet test --filter VRegAllocMeasureTests` |
| [vreg-plan-analysis.md](vreg-plan-analysis.md) | Analiza planu 37 z gałęzi `vreg/design` (kroki 1–2) | historyczna (sprzed implementacji) |
| [abi-v2-design.md](abi-v2-design.md) | Projekt ABI v2 (argumenty/wyniki w rejestrach, flaga `--abi`) | projekt; pilot `nes` istnieje — pomiar w [`../v2-sizes.md`](../v2-sizes.md) |

Zasada z [`../codegen/minic-optimization.md`](../codegen/minic-optimization.md): ABI jest zamrożone, zmiana wymaga dual-ABI albo pełnej przebudowy.
