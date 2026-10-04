# Cele, ABI i optymalizacje codegen

| Plik | Zawartość | Status |
| --- | --- | --- |
| [targets.md](targets.md) | Cele `cc --cpu`, potok kompilacji, `CpuModel`, jak dodać procesor | aktualny |
| [stub-calling-conv.md](stub-calling-conv.md) | Konwencje ABI v1/v2, ramki, runtime + kronika planów 20–38 | aktualny jako specyfikacja; sekcje planów 30–31 to historia |
| [opt-z80.md](opt-z80.md) / [opt-8080.md](opt-8080.md) / [opt-6502.md](opt-6502.md) / [opt-6800.md](opt-6800.md) / [opt-6809.md](opt-6809.md) | Stan optymalizacji per CPU (tabele ficzerów) | plany 43 i 47 zamknięte |
| [minic-optimization.md](minic-optimization.md) | Głębokość optymalizacji, limity ekosystemu, potok VReg | częściowo nieaktualny: VReg jest już zaimplementowany (plan 37) |
| [codegen-size-and-hl-plan.md](codegen-size-and-hl-plan.md) | Analiza rozmiaru vs cc65/SDCC + plan wyniku w HL | historyczny: plan 35 (wynik w HL) zrealizowany; liczby sprzed planów 32–40 |

Pomiary generowane testami zostają w root (ścieżki w testach/skryptach): [`../target-sizes.md`](../target-sizes.md), [`../compare.md`](../compare.md).
