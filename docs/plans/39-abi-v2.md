# ABI v2: argumenty i wyniki w rejestrach (projekt docs/abi-v2-design.md) (status: w kolejce)

- [ ] **1.** [S] Flaga --ir-wzorzec: --abi v1|v2 w CcCommand (default v1; v2 na CPU bez implementacji -> czytelny blad jak CTargets.Planned) + ArgRegs w CpuModels (puste = v1). Kryterium: flaga przechodzi, v2 nigdzie nie zmienia kodu (goldeny v1 nietkniete).
- [ ] **2.** [M] Pilot nes: prolog (arg1 A / A-X) + EmitCallArgs + wynik W1->A, W2->A/X + crt0 writeback do cc_ret (decyzja D3). Weryfikacja rt na nes (brak .s?). Kryterium: fib/sw/add na nes assemble+run.
- [ ] **3.** [M] Macierz nes v1 vs vreg+v2 (wartosc+konsola, wszystkie sample) + goldeny v2 (osobne pliki, wzor vreg-sizes) + bramka planu: >=15% w dol na programach wolaniowych, inaczej STOP i rewizja D2.
- [ ] **4.** [M] Kolejne CPU w kolejnosci 6502/6510/65c02 -> 6800 (A/B/X, wynik D) -> Z80/8080 (HL/DE/BC): implementacja + macierz + goldeny v2 na kazdy. stdlib .s (rt_mul/rt_div x2) przepisane na v2.
- [ ] **5.** [S] ParamAlias rejestrowy (alias parametru na rejestr argumentu, reguly zywotnosci z planu 35) albo wygaszenie na v2 + RecursionFrameTests i TailCallTests x --abi v2. Kryterium: brak regresji v1, v2 uzywa aliasow.
- [ ] **6.** [S] Decyzja o defaulcie: osobny mini-plan (macierz v2 na wszystkich CPU zielona + jakosc vs v1); ten plan konczy sie na closed z default v1.

Postęp: 0/6 gotowych.
