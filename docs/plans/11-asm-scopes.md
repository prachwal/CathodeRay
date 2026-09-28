# Asembler: zakresy leksykalne .scope/.proc (status: zamknięty)

- [x] **1.** Semantyka: .scope/.endscope, .proc/.endproc, anonimowe zakresy, eksport z zakresu — Gotowe gdy: .scope [nazwa]/.endscope, .proc nazwa/.endproc, zakres anonimowy niewidoczny z zewnątrz, dostęp z zewnątrz przez nazwa::symbol (jak ca65), .proc zamyka też zakres etykiet
- [x] **2.** Rdzeń: stos zakresów, kwalifikowane nazwy, widoczność z zewnątrz — Gotowe gdy: Pass trzyma stos zakresów, definicje kwalifikowane zakres::nazwa, lookup najpierw bieżący zakres potem globalne, tanie @ działają wewnątrz zakresu, stabilne w obu przebiegach
- [x] **3.** Rejestracja per dialekt + kolizje z mnemonikami — Gotowe gdy: .scope/.proc w stub/ca65/mos (wielkimi w mos), brak w intel/zilog? decyzja: SCOPE tylko gdzie sens — spisana; brak kolizji z mnemonikami 6502/8080/Z80
- [x] **4.** Błędy: niezbalansowanie, duplikat w zakresie, wyciek symboli — Gotowe gdy: .endscope bez .scope, niezamknięty zakres na końcu, duplikat w tym samym zakresie = błąd, odwołanie do niewidocznego = undefined; test każdego
- [x] **5.** Testy + golden z ca65 + build/test/hygiene — Gotowe gdy: testy kolizji loop w dwóch proc, dostęp zewn. przez ::, anonimowe, listing z kwalifikacją + golden Scopes/ z ca65 bajt w bajt + build -warnaserror + test + hygiene OK

Postęp: 5/5 gotowych.
