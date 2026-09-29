# Mini-C: biblioteka standardowa, preprocesor, static/const/wskaźniki do funkcji, optymalizacja (status: otwarty)

Rozmiar: S = <1 h, M = kilka h, L = dzień. Kolejność: A → B → C → D → E → F; D zależy od B (nagłówki) i C (static/const), printf od wskaźników do stałych z A.

## A. Domknięcie ograniczeń z planu 28

- [x] **1.** [S] Różnica wskaźników p - q (dzielona przez rozmiar elementu, cc_div16) i czytelny błąd dla int - ptr; sizeof(*p), sizeof(a[0]), sizeof(wyrażenie) rozstrzygane w checkerze
- [x] **2.** [M] Stałe w checkerze zamiast w parserze i codegenie: jedno miejsce składania (sizeof, enum, działania), dzięki temu sizeof(struct S) i sizeof x działają jako długość tablicy, case i inicjalizator; usunąć duplikaty Fold/TryConstValue
- [x] **3.** [M] Adresy w inicjalizatorach agregatów globalnych: emisja tablicy/struktury jako mieszanki .byte i .word symbol (uchar *names[] = {"a", "b"}, struct z polem-wskaźnikiem)
- [x] **4.** [M] Lokalne tablice i struktury w rekurencji: prolog/epilog zapisuje je na stosie (pętla PUSH/POP do 64 B) albo błąd kompilacji z wyjaśnieniem; kontrola stosu z planu 27 liczy ich rozmiar
- [x] **5.** [S] Kopiowanie struktur i lokalne inicjalizatory większe niż 255 B (licznik 16-bit)

## B. Preprocesor

- [x] **6.** [M] Makra funkcyjne #define MAX(a, b) ((a) > (b) ? (a) : (b)) (podstawianie tekstowe z nawiasami, bez rekurencji), #undef
- [x] **7.** [M] Kompilacja warunkowa: #ifdef, #ifndef, #if (stałe), #else, #endif, #error; osłony include (#ifndef X / #define X); opcja cc -D NAZWA[=wartość]

## C. Konstrukcje

- [x] **8.** [M] static: zmienna lokalna static (jedna komórka w DATA/BSS, bez ramki), globalna static i funkcja static (symbol lokalny modułu, bez .global); extern int x; między modułami (.extern cc_g_x)
- [x] **9.** [S] const: parsowanie, błąd typów przy zapisie do const zmiennej, const w parametrach wskaźnikowych (const uchar *s); tablice const trafiają do DATA
- [x] **10.** [L] Wskaźniki do funkcji: typedef void (*handler)(int); wywołanie przez zmienną i pole struktury, tablice funkcji, adres funkcji (&f, f); CALL przez łatany operand jak w crt0
- [x] **11.** [M] Typ unsigned int (uint) i porównania/dzielenie/przesunięcia bez znaku obok int ze znakiem; konwersje i ostrzeżenia mieszania

## D. Biblioteka standardowa (samples/minic/lib + include/)

- [ ] **12.** [M] Nagłówki include/string.h, ctype.h, stdlib.h, stdio.h i katalog domyślny #include <...> (cc dodaje include/ i linkuje tylko użyte moduły lib/*.c); strlen, strcpy, strncpy, strcmp, strchr, memcpy, memset, memcmp w C na wskaźnikach
- [ ] **13.** [M] stdlib: abs, min, max, atoi, itoa (podstawa 2-16), rand/srand (LFSR 16-bit); ctype: isdigit, isalpha, isspace, toupper, tolower
- [ ] **14.** [M] stdio: putchar, puts, putdec, puthex (istniejące) + printf z %d %u %x %c %s %% i do 5 argumentów wariadycznych (konwencja: liczba argumentów w cc_argN, callee czyta przez va_arg-intrinsic) oraz sprintf do bufora

## E. Jakość kodu i narzędzia

- [ ] **15.** [M] Optymalizator peephole na tekście asm przed asemblacją: STA x; LDA x, JMP na następną etykietę, LDA po LDA, martwe ładowania temp; miara: rozmiar CODE samples/minic przed/po i test, że wyniki się nie zmieniają (flaga cc -O0 wyłącza)
- [ ] **16.** [S] Ostrzeżenia checkera: nieużywana zmienna/parametr, brak return w funkcji nie-void, kod po return/break/goto, przypisanie w warunku; cc -Werror
- [ ] **17.** [S] docs/minic.md: pełna specyfikacja języka (typy, operatory, konstrukcje, konwencje, ograniczenia) zebrana z docs/stub-calling-conv.md; tabela 'wspierane / niewspierane' testowana przykładami z samples/minic

## F. Zamknięcie

- [ ] **18.** [S] Testy e2e per pozycja, samples/minic/13_strings.c, 14_macros.c, 15_funcptr.c, 16_printf.c, hygiene, aktualizacja planów

Postęp: 11/18 gotowych.
