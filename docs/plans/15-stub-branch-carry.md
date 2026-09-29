# Stub: skoki od carry BCS/BCC + divmod w lib (status: zamknięty)

- [x] **1.** Semantyka: BCS 26 / BCC 27 (a16 absolutne, 3 cykle, jak BNE/BEQ) — Gotowe gdy: BCS 26 (PC=a16 gdy C=1), BCC 27 (PC=a16 gdy C=0), a16 absolutne jak BNE/BEQ, 3 cykle, grupa jump
- [x] **2.** Rdzeń: StubOps + StubCpu + JSON + testy jednostkowe — Gotowe gdy: StubOps.Bcs/Bcc + gałęzie Execute + Supports IsAddress + wpisy JSON + HaveCount 41 + testy taken/not-taken
- [x] **3.** divmod w mathlib.s (SELF-MOD operand + BCC) + test — Gotowe gdy: divmod w mathlib.s (A=n,X=d→A=q,X=rem, dzielnik 0→0, operand SUB łatany) + test 42/5=8 r2, 0/7, 7/0
- [x] **4.** Weryfikacja asemblera + docs + build/test/hygiene — Gotowe gdy: mnemoniki przez CLI bez zmian rdzenia asemblera + build -warnaserror + test + hygiene OK

Postęp: 4/4 gotowych.
