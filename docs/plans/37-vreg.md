# VReg: wirtualne rejestry z opadaniem do Cell IR (projekt docs/vreg-design.md) (status: w kolejce)

- [ ] **1.** [S] Faza 0: flaga --ir cell|vreg (vreg -> czytelny blad) + szkielet VReg.cs; kryterium: kompiluje return 42 (design §7, §D4)
- [ ] **2.** [S] Lowering wyrazen/sterowania + VRegInterpreter + oracle vs Cell na skalarach (design §3, §8)
- [ ] **3.** [S] VRegFacts + VRegLiveness (adaptacja IrLiveness) + testy, w tym kopie regresji goto/petla (design §5, §11)
- [ ] **4.** [M] AccumulatorAllocator + VRegToCell + --ir vreg end-to-end na stub/6502/Z80 (design §4, §6)
- [ ] **5.** [M] Wolania/ABI przez adapter; TargetMatrix x vreg zielona; goldeny cell bez zmian (design §8)
- [ ] **6.** [S] Flagi nes/ioPort w Mos6502Target (N-6510 jako warianty rejestru CTargets, niezalezne od VReg; design §D6)
- [ ] **7.** [M] LinearScanAllocator + pomiar vs conservative na celach z rejestrami (design §6, §10.6)
- [ ] **8.** [S] Goldeny rozmiaru dla vreg (osobna tabela); SSA/GVN tylko pod pomiar jako osobna decyzja (design §8, §10.7)

Postęp: 0/8 gotowych.
