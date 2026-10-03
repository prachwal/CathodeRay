# Czeklista gapów architektury

Źródło analizy: odpowiedź „co jest błędne” + [`mini-c-status.md`](mini-c-status.md) §5.
Odznaczaj po dowiezieniu (listing + `compare.py` + goldeny bez wzrostu).

Flagi statusu: `closed` (zrobione), `queued` (plan istnieje, czeka), `partial` (zaczęte, wstrzymane),
`propozycja` (task bez planu — kandydat do `docs/plans/`).

## Mapa gap → plan

| Gap | Plan / task |
| --- | --- |
| DJNZ + dług splitów | plan 43 (queued, 0/8) |
| VM bytecode | plan 34 (queued, 0/4) |
| Łańcuchy `jmp` | plan 32 poz. 9 partial → P4 |
| Wynik w A/X (6502/6800) | P1 |
| ABI v2 cd., bramka −15% | P2 |
| CSE/LICM, SSA/GVN | P3 |
| Wyrocznia ramek | plan 44 poz. 6 (T1) |

## Zasada: najpierw pomiar, potem plan

Taski P1–P4 i T1 są w planie 44 ([../plans/44-gap-measurements.md](../plans/44-gap-measurements.md), queued). Wynik wpisz tu (liczba + komenda),
a dopiero na jego podstawie otwórz **jeden** plan (reguła repo: jeden plan w locie). Bez liczb nie ma planu.

- [ ] **P1 — koszt `cc_ret` na 6502/6800** (`propozycja` [S]): ile B na wołanie zjada zapis/odczyt `cc_ret`
  w `samples/minic` (listing `--cpu 6502`, `--cpu 6800`). Go: ≥15% B w programach wołaniowych → plan „wynik w A/X” (analogia planu 35).
- [ ] **P2 — re-pomiar bramki ABI v2** (`propozycja` [S]): `--abi v2` vs v1 na `05_calls` i benchach wołaniowych po planie 42
  (stary wynik +1,7 B z `v2-sizes.md` jest sprzed splitu przedziałów). Go: ≥15% → wznów plan 39 poz. 4.
- [ ] **P3 — powtórzenia i niezmienniki w IR** (`propozycja` [S]): skrypt liczący powtarzane podwyrażenia w bloku
  i niezmienniki pętli na `--ir vreg`. Go dla LICM/CSE międzyblokowego: >3% instrukcji. Bez go: SSA/GVN zostaje odłożone.
- [ ] **P4 — łańcuchy `jmp`** (`partial` [S]): re-pomiar planu 32 poz. 9 po planach 40–42 (czy +3 B na stubie nadal występuje).

## Kolejność realizacji (najpierw pomiar, potem bajty za najmniejsze ryzyko)

1. **P1–P4 + T1 (plan 44)** — równolegle, rozmiar S, zero zmiany codegenu. T1 jak najwcześniej: każda zmiana ramek/alokatora bez niego to zgadywanie.
2. **Plan 43 (DJNZ)** — pętle to trzeci koszt po wywołaniach i ramkach; ma własną bramkę go/no-go (poz. 4). Przed grzebaniem w 6502, bo rusza współdzielony alokator/liveness.
3. **Przy GO z fazy 0, po stosunku zysk/koszt**: wynik W≤2 w A/X na 6502 (GO z P1) → ParamAlias rejestrowy + re-bramka v2 (GO z P2) → LICM prostych niezmienników (GO z P3) → łańcuchy `jmp` (GO z P4).
4. **Ogony i 6800** (małe, niezależne): ParamAlias BE + tail call 6800 (każde po micro-pomiarze) → tail call z ramką (rozpoznanie) → relaksacja form w asemblerze (największe ryzyko regresji, na końcu).
5. **Tor równoległy**: plan 34 (VM) — niezależny od codegen AOT, nie konkuruje o priorytet. **Koniec kolejki**: alokator grafowy (dopiero gdy bramka linear-scan vs greedy pokaże lukę).

Nie zaczynać od ABI v2 ani SSA „bo to właściwe rozwiązanie” — oba mają przegrane bramki (v2: +1,7 B; SSA: brak pomiaru).

## Bez pomiaru nie ruszać (decyzje)

- **Emiter VReg `--regalloc` i alokator grafowy** — keep-set jest doradczy; diagram: [architecture-diagram.md](architecture-diagram.md) §1.
  Zależy od wyniku P3 i bramki linear-scan vs greedy; teraz nie planować.
- **ParamAlias BE (6800), tail call 6800 / z ramką** — małe zyski, ryzyko ramek; po P1.
- **Forward-ref = najdłuższa forma** — relaksacja częściowa; zrobić tylko jeśli `compare.py` pokaże ≥1% na asemblerze.
- **Ślepa wyrocznia ramek** — jedyny gap jakości, nie rozmiaru: `IrInterpreter` powtarza semantykę `Saved`.
  Kandydat na osobny plan testowy [M]: niezależny model ramek w teście albo fuzz mutujący `Saved` (klasa `646d204`).

## Odłożone / kolejka (plany istnieją)

- [ ] `queued` Plan 43 (DJNZ, 0/8).
- [ ] `queued` Plan 34 (VM bytecode, 0/4).
- [ ] SSA / GVN — `propozycja` dopiero po pomiarze z sekcji IR.

## Zamknięte (referencja, nie odznaczać)

- [x] `closed` Ramki vs skoki w przód (`646d204`, `RecursionFrameTests`) — plan 30/32.
- [x] `closed` VRegLift: kill-bez-pokrycia, `__end`, addr-taken → `Pinned` — plan 37.
- [x] `closed` Wynik w HL (Z80/8080) — plan 35. Tail call Z80/8080/6502 — plan 36/40.
- [x] `closed` Pilot ABI v2 `nes` + flaga `--abi` — plan 39 (bramka nie przeszła, default v1).
