# Stub: podział opcode na grupy + pełny zestaw ~30 (status: zamknięty)

- [x] **1.** Podział StubOps na pliki grupowe (System/Load/Arithmetic/Logic/Branch/Compare/Stack) — Gotowe gdy: StubOps.cs podzielony na partial per grupa JSON (System/Load/Arithmetic/Logic/Jump/Compare/Stack), brak zmiany semantyki, build zielony
- [x] **2.** Transfery TAX/TXA + DEC/DEX + CLC/SEC + BEQ — Gotowe gdy: TAX TXA DEC DEX CLC SEC BEQ działają, Z/C/V wg konwencji (transfer/DEC/DEX:Z; CLC/SEC:C; BEQ: skok gdy Z=1)
- [x] **3.** Logika AND/ORA/EOR (d8 i a16,X) — Gotowe gdy: AND ORA EOR w d8 i a16,X, wynik do A, tylko Z (C/V bez zmian)
- [x] **4.** Stos: SP w StubState + PUSH/POP/CALL/RET — Gotowe gdy: SP byte w StubState (Reset 0xFF, Clone, CaptureRegisters), PUSH/POP/CALL/RET na stronie 0100h, LIFO, RET wraca po CALL
- [x] **5.** JSON ISA + asembler StubSet + rejestry — Gotowe gdy: nowe wpisy w mcp_stub_instructions.json (0x11+), StubCpu.Supports/Execute, brak kolizji mnemoników w StubSet
- [x] **6.** Testy StubOps/StubCpu + sample + build/test/hygiene — Gotowe gdy: HaveCount 18->30, testy każdej nowej instrukcji, sample z podprogramem, build -warnaserror + test + hygiene OK

Postęp: 6/6 gotowych.
