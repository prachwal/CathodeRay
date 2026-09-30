# Mini-C: maszyna wirtualna (bytecode + interpreter) (status: w kolejce)

- [ ] **1.** Zamrożenie kodowania IR (spec, serializacja, wersja formatu) — Gotowe gdy: spec kodowania ~30 opów IR w docs/bytecode.md, format cathode-vm/1, round-trip serializacji w testach
- [ ] **2.** Interpreter w C# (fetch-decode-execute) + cathode vm run — Gotowe gdy: interpreter wykonuje moduły .cvm (na bazie IrInterpreter), komenda cathode vm run prog.cvm działa na korpusie
- [ ] **3.** Bramka fuzzing: bytecode vs AOT (zgodność) — Gotowe gdy: fuzzing różnicowy bytecode-vs-AOT na wszystkich celach zielony (jak leaf_fuzz vs gcc)
- [ ] **4.** Testy + docs + build/test/hygiene — Gotowe gdy: testy + docs + build -warnaserror + test + hygiene OK

Postęp: 0/4 gotowych.
