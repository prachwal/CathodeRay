# Przykłady zgodności z C

Po jednym programie na funkcjonalność dodaną w ramach planu 48 (`docs/plans/48-lisp-compat.md`). Oczekiwania są w
komentarzach `//` na początku pliku, a `tests/CathodeRay.Tests/CompatSamplesTests.cs` kompiluje każdy program
i wykonuje go na emulatorach (domyślnie z80, 8080, 6502, 6800, 6809).

| Dyrektywa | Znaczenie |
|---|---|
| `// expect-result: N` | wartość zwrócona przez `main` (16 bitów, ze znakiem) |
| `// expect-console: tekst` | tekst wypisany na konsolę (`__io_buf`) |
| `// expect-args: …` | dodatkowe argumenty `cathode cc` (np. `-D CC_DOUBLE64`) |
| `// cpus: z80 8080` | ogranicza listę procesorów |

| Plik | Funkcjonalność |
|---|---|
| `a1_include_comment.c` | komentarze `/* */` i `//` w dyrektywach preprocesora |
| `a2_variadic_macros.c` | makra wariadyczne (`...`, `__VA_ARGS__`) |
| `b1_array_typedef.c` | `typedef` typu tablicowego (jednowymiarowy i dwuwymiarowy, parametr, sizeof) |
| `c1_stdint.c` | `<stdint.h>`: typy o stałej szerokości, zawijanie, znak |
| `c2_setjmp.c` | `<setjmp.h>`: setjmp/longjmp na wszystkich celach |
| `c3_stdio.c` | `<stdio.h>`: FILE, stdin/stdout/stderr, fprintf, snprintf, getc, ungetc, fopen |
