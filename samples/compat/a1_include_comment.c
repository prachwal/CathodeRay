// A1: komentarze blokowe i liniowe w dyrektywach preprocesora (po #include, #define, #if) oraz "//" w napisie.
// expect-result: 46
#include <string.h> /* komentarz blokowy po include */
#include <stddef.h> // komentarz liniowy po include
#include <ctype.h> /* komentarz wielowierszowy
                      zaczęty w dyrektywie
                      i kończący się dalej */

#define FACTOR (6 /* wartość */ + 1)
#define URL "a//b"
#if defined(FACTOR) /* warunek */
#define PICK 35
#else
#define PICK 0
#endif

int main(void)
{
    return strlen(URL) + FACTOR + PICK;
}
