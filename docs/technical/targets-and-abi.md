# Cele i konwencja wołań

Rejestr celów: `CTargets.All`. Źródła: [../targets.md](../targets.md), [../stub-calling-conv.md](../stub-calling-conv.md).

## Cele

| cel | CPU | kolejność bajtów | argumenty / wynik | interpreter w testach |
| --- | --- | --- | --- | --- |
| `stub` | zaślepka CathodeRay (A, X) | LE | argumenty w A/X i `cc_argN`, wynik w A/X | `StubCpu` |
| `6502`, `65c02`, `nes`, `6510` | MOS 6502, WDC 65C02, NES 2A03, MOS 6510 | LE | argumenty `cc_argN`, wynik `cc_ret`, wskaźnik `(__p),Y` na stronie zerowej | `Mos6502` |
| `z80` | Zilog Z80 | LE | argumenty `cc_argN`, wynik W≤2 w HL (L dla 1 B), HL jako rejestr adresowy | `Z80Cpu` |
| `8080` | Intel 8080 | LE | jak `z80`, mnemoniki Intel | `Z80Cpu(intel8080: true)` |
| `6800` | Motorola 6800 | BE | argumenty `cc_argN`, wynik `cc_ret`, rejestr X | `Mc6800Cpu` |

Warianty `65c02`, `nes` i `6510` to flagi tej samej klasy `Mos6502Target`, a nie osobne selektory. Mają ten sam kod co `6502`,
a różnią się układem pamięci i crt0 (np. `nes` bez BCD, `6510` z portem I/O). Rozmiary kodu `nes` i `6510` są co do bajta równe `6502`.

## Konwencja wołań (ABI v1)

ABI jest zamrożone. Zmiana konwencji (np. wynik w HL zamiast A/X) wymaga przebudowy wszystkich modułów, ręcznego asemblera i runtime,
więc zmiany przechodzą przez okres podwójnego ABI albo pełną przebudowę.

- Argumenty trafiają do komórek `cc_arg1`…`cc_arg6` (+ `_h` dla starszego bajtu `int`), maksymalnie 6.
  Na stubie pierwszy argument idzie w `A` (int: `A`=lo, `X`=hi).
- Wynik: `cc_ret` (+ `_h`) na 6502 i 6800, w `HL` na Z80 i 8080 (plan 35), w `A`/`X` na stubie.
- Caller niczego nie zachowuje, a callee zachowuje własne komórki (parametry, lokale, tempy) na stosie sprzętowym w prologu i odtwarza je w epilogu.
  Dzięki temu rekurencja działa do wyczerpania stosu (stub: ok. 256 B).
- Globale są współdzielone i nie są odkładane.
- Komórki umówione i symbole `__bss_start`/`__bss_end` definiuje crt0, więc moduły C ich nie emitują.

### Układ pamięci stuba

CODE od `$1000` (24 KB), BSS od `$7000` (4 KB), DATA od `$8000`. crt0 zeruje BSS od `__bss_start` do `__bss_end`.

### Crt0

Ustawia stos, zeruje BSS, woła procedury z tablicy INIT, woła `main` i zatrzymuje CPU (pętla na sobie samej albo HALT).
Definiuje `cc_arg1..6(+_h)`, `cc_ret(+_h)`, `cc_rethi`, `cc_retbuf`, `cc_t0`, `cc_t1` i pomocnika wywołań pośrednich.

### Konsola i ekran (stub)

Konsola to umowa testowa: stały bufor `__io_buf` (256 B) i kursor `__io_cur` z `samples/stub/lib/io.s`.
Ekran 40×25 to bufor `__scr_buf` (1000 B) w `screen.s`. Host dekoduje go poleceniem `cathode stub run ... --screen-at ADDR`.

## Model CPU (`CpuModel`, plan 38)

Jawny kontrakt rejestrowy dla optymalizatora (`src/CathodeRay.C/Cpu*.cs`):

- rejestry i aliasy (np. `hl=[h,l]`, pary `bc`, `de`),
- co niszczy wywołanie (`ClobberedByCall`) i czym „drapią” prymitywy (`Scratch`),
- gdzie wraca wynik (`ResultReg`) i dokąd idą argumenty (`ArgRegs`; dziś puste, czyli ABI v1),
- efekty prymitywów (`PrimEffects`: czytane i zapisywane rejestry, flagi).

Selektor czyta model zamiast zgadywać (zajętość przez aliasy, komórki argumentów, walidacja przydziału rejestrów).
Efekty prymitywów są sprawdzone fuzzem różnicowym przeciw emulatorom (`PrimEffectsFuzzTests`, ok. 9 tys. sekwencji).
Fuzz znalazł przy okazji błędy w emulatorach (RLA/RRA bez flag S/Z/P, STAA ruszający N/Z/V), które naprawiono.

## Jak dodać procesor

1. **Asembler**: wpis w `AssemblerTargets` (plik ISA JSON, dialekt składni, kolejność bajtów). Linker zna relokacje `Abs16`, `Rel8`, `Lo8`, `Hi8`.
2. **Prymitywy**: klasa `XxxIsa : ByteIsa` (ok. 150 linii) z `LoadA`/`StoreA`, `Alu`, `Cmp`, `ShlA`/`ShrA`, skokami, stosem,
   `Call`/`Return`, obsługą wskaźników, `Crt0()` i składnią danych. Nazwy zastrzeżone (rejestry, operatory) trafiają do `Reserved`.
   Symbole użytkownika o takich nazwach dostają przedrostek `cc_r_`.
3. **Cel**: `XxxTarget : ByteTarget` i wpis w `CTargets.All`.
4. **Crt0**: ustawienie stosu, BSS, INIT, `main`, zatrzymanie.
5. **Testy**: interpreter w `tests/` (`ICpuRunner`, rejestr `Runners`). Wtedy `IrConformanceTests`, `TargetMatrixTests` i testy funkcji
   uruchamiają się na nowym celu same. Odstępstwa od konwencji opisz w `stub-calling-conv.md`.
6. **Model**: wpis w `CpuModels`. Testy `CpuModel*Tests` pilnują zgodności z emiterem.

## Znane ograniczenia celów

- Komórki leżą w pamięci absolutnej (na 6502 najczęściej używane skalary i wskaźniki trafiają na stronę zerową),
  więc kod jest większy niż pisany ręcznie.
- Skoki warunkowe 6502 i 6800 są relaksowane: krótki skok, gdy cel jest w zasięgu, inaczej odwrócony skok i `JMP`.
- `float`, `long long` i dzielenie lub mnożenie 32-bitowe są programowe, więc wolne. `float` i `long long` na stubie (24 KB kodu) mieszczą się tylko w małych programach.
