# Mini-C: frontend (lexer/parser podzbioru C) (status: zamknięty)

- [x] **1.** Gramatyka podzbioru C (deklaracje, wyraż., sterowanie, funkcje) — Gotowe gdy: spisana gramatyka: uchar/int, funkcje, if/else, while, for, return, operatory +,-,*,/,%,<<,>>,==,!=,<,>,<=,>=,&,|,^,~,!,&&,||, przypisanie, wywołania
- [x] **2.** Lexer + parser z błędami linia:kolumna — Gotowe gdy: lexer (liczby dec/hex, identy, operatory, komentarze) + parser rekurencyjny z błędami linia:kolumna jak AssemblerError
- [x] **3.** AST + pretty-print/debug dump — Gotowe gdy: AST (FuncDecl, VarDecl, If, While, For, Return, Binary, Unary, Call) + ToString do testów snapshot
- [x] **4.** Testy: poprawne i błędne programy + build/test/hygiene — Gotowe gdy: 10+ programów poprawnych i 10+ błędnych (każdy błąd z pozycją) + build -warnaserror + test + hygiene OK

Postęp: 4/4 gotowych.
