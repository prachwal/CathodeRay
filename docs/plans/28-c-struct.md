# Mini-C: struct, typedef, goto, wskaźniki w zwrocie, inicjalizatory globalne (status: otwarty)

Rozmiar: S = <1 h, M = kilka h, L = dzień. Kolejność: A → B → C → D → E.

## A. Małe i szybkie

- [x] **1.** [S] typedef: alias typu (uchar/int/void, wskaźniki, struct) w parserze; tablica aliasów, bez nowych węzłów AST
- [x] **2.** [S] goto i etykiety: label: w funkcji, JMP do etykiety z prefiksem funkcji; checker: brak etykiety / duplikat; goto do przodu i do tyłu

## B. Wskaźniki i przypisania

- [x] **3.** [M] Funkcje zwracające wskaźnik: Ast.Function z PointerDepth zwrotu, CType zwrotu w checkerze (return, wywołanie, prototyp), ReturnsInt -> szerokie (int lub ptr), test T* f() + linkowanie z prototypem
- [ ] **4.** [M] Złożone przypisanie i ++/-- na celu ze skutkami ubocznymi (a[i++] += 1, *p++ += 1): węzeł AssignOpTo liczący adres raz (EvalPtrAddr -> temp, PatchedLoad, op, PatchedStore); usunąć RequirePure

## C. Inicjalizatory globalne

- [ ] **5.** [S] Składanie stałych w checkerze: sizeof(zmienna/typ), stałe enum, &g + stała, stała arytmetyka na globalnych const-init; wynik jako Number w init (bez kodu startowego)
- [ ] **6.** [L] Inicjalizator niestały (int x = f(); int y = x + 1;): segment INIT z tablicą .word procedur __cc_init_<moduł>, crt0 woła je po zerowaniu BSS pętlą do __init_end (symbol linkera z planu 27), kolejność = kolejność modułów; mapa .cfg i origins testów dostają INIT

## D. struct (na końcu, największe)

- [ ] **7.** [M] Deklaracje struct S { pola }; i typ struct S: układ pól (offsety, rozmiar, bez wyrównania), CType.Struct, sizeof(struct S), zmienne lokalne/globalne/tablice struktur w BSS
- [ ] **8.** [M] Dostęp do pól: s.f i p->f jako Index/Deref z przesunięciem (adres bazy + offset stały), odczyt i zapis uchar/int/ptr, pola-tablice, zagnieżdżone struct; ++/+= na polach przez AssignOpTo
- [ ] **9.** [M] Struktury przez wskaźnik: &s, p->f, wskaźnik do struktury w argumentach i zwrocie (zależy od zwracania wskaźników); kopiowanie s1 = s2 pętlą bajtów; przekazanie i zwrot przez wartość zabronione komunikatem
- [ ] **10.** [S] Inicjalizatory struct { a, b } (lokalne i globalne w DATA), zerowanie reszty

## E. Zamknięcie

- [ ] **11.** [S] Testy e2e per pozycja, samples/minic/10_struct.c, 11_goto_typedef.c, 12_globals_init.c, docs/stub-calling-conv.md, hygiene

Postęp: 3/11 gotowych.
