# Optymalizacje codegen: Zilog Z80

Stan na main po planach 40–45. Plan 43 (DJNZ) i plan 45 (łańcuchy `jmp`) są zamknięte. Model: A + pary BC/DE/HL, wynik w HL, flaga V (`HasOverflowFlag`).
ABI v2 nie dotyczy (pilot `nes`, flaga `--abi`).

| Ficzer | Status | Dowód / uwagi |
| --- | --- | --- |
| Rejestry komórek (alokator) | zrobione | `CellRegisters`/`CellPairs` + `RegisterAllocator.Tune`; parametry w puli (plan 40, item 9-część); `SavedAround` push/pop; testy `Z80RegisterIsaTests`, `ParamRegisterTests`; dzielenie przedziałów nie przecina krawędzi przepływu (`FlowsBetweenRuns`, test `RegisterAllocatorTests`) |
| ParamAlias (liście → `cc_argN`) | zrobione | Jak wyżej: globalne pomijanie odrzucone po regresji |
| Wynik w rejestrze | zrobione | `ReturnsInResultReg`; `TryMoveTo/FromResultReg`; zamiana do DE przez `ex de,hl` |
| Kopie słów (`TryMoveWord`) | zrobione | Przez HL; `ResultRegFreshTests` (świeży wynik bez przeładowania) |
| Arytmetyka słów (`TryAddWord`) | zrobione | `add hl,de` / `or a; sbc hl,de`; stałe ±1..3 przez `inc/dec hl`; epilog `ex de,hl` do pary DE + flaga `FreshAddInHL` |
| Shift słowa (`TryShlWord1`) | zrobione | `add hl,hl`; hak `Add(x,x)` w selektorze i hak `Shl 1` w `EmitShift` |
| Porównania ze znakiem (forma zerowa) | zrobione | `TryBranchZeroSigned` |
| Porównania ze znakiem (forma ze stałą) | zrobione | `TryBranchSignedConst` (`Lt/Ge` wprost, `Le/Gt` przez +1; pusta etykieta else gdy V idzie na spadek); bateria `Z80SignedCompareTests` (60 stałych × ~310 wartości, INT_MIN..INT_MAX) |
| Peephole `Tidy` | zrobione | Redundant/dead `ld` BC/DE↔HL; `ld a,0`→`xor a` po labelce/bezwarunkowym `call` (guard: flagi nieokreślone); `SwapReloadDe` (cofa `ex`+reload do kopii, para do `RedundantDeToHl`) |
| Relaksacja skoków | zrobione | `Shorten` (`jp`→`jr` w zasięgu), `DropJumpToNext`, `SkipOverJump`+`Invert` |
| Tail call | zrobione | `jp` + `jp (hl)`; `TailCallTests` (w tym mutual-recursion bez ramki); martwa stopka pomijana |
| Ramki (`Saved`, push/pop) | zrobione | `TryPushWord`/`TryPopWord` + `keepResult`; `SavedAround`; `RecursionFrameTests` |
| Wskaźniki | częściowe | `PtrSetup`/`PtrLoad`/`PtrStore` zrobione; brak `IndexFusion` (tylko 6502 ma `SupportsIndexed`) |
| Kopiowanie bloków (LDIR) | zrobione | Matcher pętli w selektorze + `TryCopyLoop` (test zera, `push bc/de`, `ldir`); `CopyLoopTests` (wartości, n=0, `Le`, elementy W2 zostają pętlą) |
| Strength reduction | zrobione | Jak wyżej + mnożenie przez stałą w `Legalizer`; `twice` 9→5 B |
| Switch `uchar` → 1 B | zrobione | Zwężenie w `LowerSwitch` (wspólne); `SwitchNarrowTests` |
| DJNZ | zrobione | Plan 43: licznik W1 pre-kolorowany do B; `djnz`, a dla dalekiego ciała `dec b` + `jp nz`; `DjnzTests` |
| RST dla helperów | brak | Świadomie odroczone: ~5 site'ów w `div16` (wiersz spoza średniej) za cenę strony 0, wektorów, emulatora i harnessu |
| ABI v2 (rejestry) | brak | Nie dotyczy |

## Braki / next

- `IndexFusion` dla Z80 (tryb `(HL)` już jest; chodzi o `LoadIdx`/`StoreIdx` tuż przed selektorem).
