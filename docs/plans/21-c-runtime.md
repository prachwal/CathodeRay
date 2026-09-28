# Mini-C: crt0 i preprocesor (status: w kolejce)

- [ ] **1.** crt0: zerowanie .bss, SP=FF, skok do main, HLT po powrocie — Gotowe gdy: crt0.s: zerowanie .bss (pętla), SP=FF, CALL main, HLT; linkowany zawsze pierwszy
- [ ] **2.** Preprocesor: #include→.include, #define→.define (mapowanie 1:1) — Gotowe gdy: #include "f" → .include, #define X v → .define (proste, funkcyjne odrzucone z błędem jak w ca65-doc)
- [ ] **3.** Testy runtime + build/test/hygiene — Gotowe gdy: program C startuje od crt0 w teście runtime + build -warnaserror + test + hygiene OK

Postęp: 0/3 gotowych.
