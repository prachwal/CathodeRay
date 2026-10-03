# Języki programowania — trudność implementacji frontendu

Dokument opisuje przybliżoną trudność dodania kolejnych frontendów językowych do CathodeRay. Skala dotyczy implementacji kompilatora w istniejącej architekturze CathodeRay, a nie trudności nauki języka.

## Ranking

| Poziom | Język | Trudność | Główna przeszkoda |
| ---: | --- | ---: | --- |
| 1 | BASIC | 2/10 | prosty parser, typowanie zależne od dialektu |
| 2 | Pascal | 3/10 | prosty system typów, rekordy, tablice |
| 3 | Forth | 3/10 | bardzo prosty parser, nietypowy model stosowy |
| 4 | Lua | 4/10 | typowanie dynamiczne, tabele, domknięcia |
| 5 | Oberon | 4/10 | prosty i spójny projekt języka |
| 6 | Ada — podzbiór | 5/10 | silne typowanie, zakresy, rekordy |
| 7 | C | 5/10 | wskaźniki, pamięć, preprocesor, ABI |
| 8 | Fortran 77 — podzbiór | 6/10 | tablice, fixed form, starsze konstrukcje |
| 9 | C++ — podzbiór | 7/10 | przeciążenia, klasy, szablony |
| 10 | COBOL | 7/10 | DATA DIVISION, PIC, liczby dziesiętne, REDEFINES |
| 11 | Fortran 95/2003 — podzbiór | 8/10 | tablice dynamiczne, CHARACTER, typy pochodne |
| 12 | Rust — podzbiór | 8/10 | ownership, borrowing, lifetimes |
| 13 | Java/Kotlin | 8/10 | GC, obiekty, runtime, wyjątki |
| 14 | C++ — współczesny | 9/10 | bardzo duża semantyka języka |
| 15 | Fortran — pełny | 10/10 | złożona semantyka tablic, procedur i typów |
| 16 | COBOL + dialekty | 10/10 | historyczne konstrukcje i różnice dialektów |

## Co warto dodać najpierw

### 1. Pascal

Pascal jest szczególnie dobrym kolejnym frontendem dla CathodeRay. Pozwala przetestować:

- rekordy,
- tablice,
- wskaźniki,
- enumeracje,
- zakresy,
- procedury i funkcje,
- typy złożone,

bez wchodzenia od razu w problemy charakterystyczne dla współczesnego Fortranu lub COBOL-a.

### 2. BASIC

Najprostszy frontend do szybkiego uruchomienia. Dobrze nadaje się do testowania backendów 8-bitowych i prostych konstrukcji sterujących.

### 3. Forth

Interesujący test architektury VReg, ponieważ semantyka źródłowa jest oparta na stosie. Wymusza świadome rozdzielenie modelu wykonania języka od reprezentacji backendowej.

### 4. C

Naturalne rozszerzenie obecnego Mini-C. Pozwala zwiększyć zakres języka bez konieczności wprowadzania całkowicie odmiennego modelu semantycznego.

### 5. Fortran

Dobry test tego, czy VReg jest rzeczywiście językowo niezależnym backendowym IR. Fortran wymaga jednak osobnego Fortran Semantic IR przed obniżeniem do VReg.

### 6. COBOL

Bardzo dobry test modelu pamięci, layoutu danych i operacji dziesiętnych. Powinien korzystać z osobnego COBOL Semantic IR przed VReg.

## Zalecana kolejność rozwoju CathodeRay

**BASIC → Pascal → C → Fortran → COBOL**

Następnie można rozważyć:

**Lua → Ada → Forth → Rust → C++**

Taka kolejność zwiększa stopniowo wymagania wobec parsera, systemu typów, modelu pamięci, IR i backendu.

## Znaczenie dla VReg

Najważniejszy wniosek architektoniczny:

> VReg nie powinien być Mini-C IR. Powinien być ogólnym, typowanym backendowym IR dla wielu frontendów.

Docelowy model:

```text
             ┌── Mini-C ──────────┐
             │                    │
             ├── BASIC ───────────┤
             │                    │
             ├── Pascal ──────────┤
             │                    │
             ├── Fortran ─────────┤
             │                    │
             └── COBOL ───────────┘
                       ↓
             Language-specific IR
                       ↓
                    VReg IR
                       ↓
                SSA / passes
                       ↓
              register allocation
                       ↓
                 target emission
                       ↓
          Z80 / 6502 / 6800 / ...
```

### Zasada

**Semantyka języka należy powyżej VReg. Problemy maszynowe i zasoby sprzętowe należą poniżej VReg.**

Dzięki temu dodanie nowego języka powinno przede wszystkim wymagać implementacji jego frontendu i semantic IR, a nie kolejnego niezależnego backendu dla każdego CPU.

## Wymagania języków wobec IR, runtime i bytecode

Tabela wiąże ranking z tym, czego dziś brakuje w repo. Kolumna „Repo ma" pochodzi z [`../technical/mini-c-status.md`](../technical/mini-c-status.md)
i `src/CathodeRay.C/stdlib`; kolumna „Brakuje" to wnioski z opisu języka (wiedza ogólna, do potwierdzenia przy pisaniu planu frontendu).

### Wspólne klocki

| Klocek | Repo dziś | Potrzebują |
| --- | --- | --- |
| Sterta (`malloc`/`free` lub pula) | brak (`stdlib.h` bez alokatora) | BASIC (napisy dynamiczne), Pascal (`new`/`dispose`), Forth (słownik), Lua, Java/Kotlin, Rust (`Box`) |
| Zmienna liczba argumentów | tylko deklaracja `...`, `printf` do 5 argumentów | C (pełny), Pascal `write`, BASIC `PRINT` z listą, Fortran `WRITE` |
| Rzeczywiste I/O | konsola jako bufor `__io_buf`, bez urządzeń | wszystkie (`INPUT`, `READ`, `ACCEPT`, `KEY`); usługi `SYS n` z planu 34 poz. 4 |
| `float` | jest (`rt_float.c`, `ftoa.c`) | BASIC, Pascal `real`, Fortran `REAL`, Lua |
| `long long` | jest (`rt_ll64.c`) | Pascal `int64`, Fortran `INTEGER*8`, COBOL `COMP` do 18 cyfr |
| Struktury, tablice, wskaźniki na funkcje, `switch`, `goto` | są | Pascal `record`, Forth `EXECUTE`, BASIC `ON..GOTO`, COBOL `GO TO DEPENDING` |
| Dziesiętne (BCD, packed) | brak | COBOL (`PIC`, `COMP-3`), opcjonalnie BASIC |
| Deskryptory tablic i napisów z długością | brak | Fortran (assumed-shape, `CHARACTER`), Pascal (napisy), BASIC |
| Link statyczny (zagnieżdżone procedury) | brak | Pascal, Ada, Oberon |
| Wartości otagowane i GC | brak | Lua, Java/Kotlin |
| Wyjątki, domknięcia, generyki | brak | Ada, Rust, C++, Lua |

### Co daje który język

| Język | Co jest łatwe (IR już to ma) | Co trzeba dodać | Droga A (kompilator) czy B (interpreter w mini-C) |
| --- | --- | --- | --- |
| **BASIC** | pętle `FOR`, `IF`, `GOTO` jako etykiety, `ON..GOTO` jako `switch`, liczby, konsola | `GOSUB`/`RETURN` (stos adresów albo wołania), napisy (pula lub sterta), `DATA`/`READ` (tablica statyczna), `PEEK`/`POKE` (surowa pamięć, tylko dla jednego komputera), `INPUT` | A dla BASIC-a z typami w sufiksach (`A%`, `A$`); B dla `RUN`, `EVAL`, REPL |
| **Pascal** | rekordy, tablice, wskaźniki, enumeracje, `case`, procedury, funkcje | zagnieżdżone procedury (link statyczny), parametry `var` (wskaźniki), napisy z długością, zbiory, kontrola zakresów | A |
| **C (pełny)** | już prawie cały podzbiór | zmienna liczba argumentów, `malloc`, unie (do sprawdzenia w `minic.md`) | A (to ten sam frontend) |
| **Forth** | wywołania, stos jako tablica, `EXECUTE` jako wskaźnik na funkcję | słownik dynamiczny (pula pamięci), `DOES>`, `KEY`/`EMIT` przez `SYS` | A dla podzbioru statycznego, B dla pełnego Forth z definiowaniem słów w trakcie działania |
| **Fortran** | pętle `DO`, tablice jawne, `float`, `long long` | deskryptory tablic (assumed-shape), `CHARACTER` z długością, `COMMON`, I/O sformatowane, układ kolumnowy tablic (opis w [fortran-implementation.md](fortran-implementation.md)) | A przez Fortran Semantic IR → VReg |
| **COBOL** | pola jako bajty, pętle `PERFORM`, `GO TO` | `PIC`, `COMP-3` i arytmetyka dziesiętna (osobny runtime), `REDEFINES`, `OCCURS`, pliki ([cobol-implementation.md](cobol-implementation.md)) | A przez COBOL Semantic IR → VReg |
| **Lua** | pętle, funkcje | wartości otagowane, tablice, domknięcia, GC | B (interpreter), po pojawieniu się sterty |
| **Ada, Rust, C++ (podzbiory)** | struktury, wołania | zakresy i wyjątki (Ada), własność i czas życia (Rust, głównie w frontendzie), szablony i klasy (C++) | A, po wcześniejszych etapach |

### Konsekwencje dla bytecode (plan 34)

- Język, który obniża się tylko do operacji IR już istniejących (BASIC z typami, Pascal bez zagnieżdżeń, C), **działa na maszynie wirtualnej bez zmian formatu**.
- Język potrzebujący nowych operacji (link statyczny, deskryptory, arytmetyka dziesiętna, wartości otagowane) wymaga **rozszerzenia formatu** `cathode-vm/1`. Dlatego format musi mieć wersję i mechanizm rozszerzeń już w poz. 1 planu 34.
- Wymagania I/O są wspólne dla wszystkich języków: jedna lista usług `SYS n` (plan 34, poz. 4) obsłuży je wszystkie, zamiast osobnych rozwiązań per język.
- Sterta jest najczęstszą brakującą rzeczą (BASIC, Pascal, Forth, Lua, Rust, Java). Warto ją rozstrzygnąć jednym małym planem (pula z `malloc`/`free`) **przed** frontendami, bo odblokowuje cztery z nich.
