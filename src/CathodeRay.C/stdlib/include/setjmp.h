/* setjmp.h — skoki nielokalne. jmp_buf trzyma wskaźnik stosu po powrocie z setjmp i adres powrotu (6 bajtów wystarcza
   na każdym celu). Funkcja wołająca setjmp nie trzyma swoich komórek w rejestrach (alokator), więc po longjmp widzi
   ich ostatnie wartości; stan wewnątrz zdjętych ramek przepada, jak w C. */
#ifndef _SETJMP_H
#define _SETJMP_H
typedef int jmp_buf[3];
int setjmp(jmp_buf env);
void longjmp(jmp_buf env, int val);
#endif
