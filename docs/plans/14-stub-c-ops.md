# Stub: opcode pod podstawowe C (przesunięcia, NOT, CPA) (status: zamknięty)

- [x] **1.** Semantyka: SHL/SHR/NOT/CPA (flagi, cykle) + decyzja MUL/DIV/ROL/ROR — Gotowe gdy: SHL 22 (A<<1, C=bit7, Z), SHR 23 (A>>1, C=bit0, Z), NOT 24 (A=~A, Z), CPA 25 (A-d8 bez zapisu: Z/C jak CPX); cykle 2/2/1/2; MUL/DIV: tylko MUL jako lib (DIV niemożliwy bez BCS/BCC — udokumentowane)
- [x] **2.** Rdzeń: StubOps + StubCpu + JSON + testy jednostkowe
- [x] **3.** Asembler: weryfikacja mnemoników + golden
- [x] **4.** Biblioteka MUL/DIV (sample lib) albo opcode — Gotowe gdy: samples/stub/features/mathlib.s z mul8 (A=a,X=b→A, test 6*7=42 i 0*5=0) + test; DIV opisany jako luka (propozycja BCS/BCC follow-up)
- [x] **5.** Docs ISA + build/test/hygiene

Postęp: 5/5 gotowych.
