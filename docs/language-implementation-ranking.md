# Języki programowania — trudność implementacji frontendu

Dokument opisuje przybliżoną trudność dodania kolejnych frontendów językowych do CathodeRay. Skala dotyczy implementacji kompilatora w istniejącej architekturze CathodeRay, a nie trudności nauki języka.

## Ranking

| Poziom | Język | Trudność | Główna przeszkoda |
|---:|---|---:|---|
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
