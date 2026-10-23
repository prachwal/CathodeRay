// C4: <stdlib.h> strtod/atof, sscanf z %g/%lg i %n oraz snprintf z %g/%f (float; flagi i precyzja pomijane).
// Precyzja to float (CC_DOUBLE64 zmienia tylko rozmiar double), więc wyniki porównujemy po przeskalowaniu do całkowitych.
// expect-result: 76
// expect-console: 125|-25|12|3|2.5|0.1|100|1.500000|abc
#include <stdio.h>
#include <stdlib.h>

int main(void)
{
    uchar buf[24];
    uchar *end;
    float f;
    int n = 0;
    int ok = 0;
    double d;

    d = strtod("12.5e1xyz", &end);                    /* 125, end -> "xyz" */
    snprintf(buf, 24, "%d|", (int)d);
    fputs(buf, stdout);
    ok += end[0] == 'x';

    d = atof("-0.25");
    snprintf(buf, 24, "%d|", (int)(d * 100));         /* -25 */
    fputs(buf, stdout);

    d = strtod("  12abc", &end);
    snprintf(buf, 24, "%d|", (int)d);
    fputs(buf, stdout);
    ok += end[0] == 'a';

    d = strtod("nic", &end);                           /* brak cyfr: 0 i end na początku */
    ok += d == 0 && end[0] == 'n';

    sscanf("3.75 rest", "%f%n", &f, &n);
    snprintf(buf, 24, "%d|", (int)f);
    fputs(buf, stdout);
    ok += n == 4;

    snprintf(buf, 24, "%g|", 2.5f);                    /* 2.5 */
    fputs(buf, stdout);
    snprintf(buf, 24, "%.17lg|", 0.1f);                /* precyzja pomijana: 0.1 */
    fputs(buf, stdout);
    snprintf(buf, 24, "%g|", 100.0f);                  /* 100 */
    fputs(buf, stdout);
    snprintf(buf, 24, "%f|", 1.5f);                    /* 1.500000 */
    fputs(buf, stdout);
    fputs("abc", stdout);
    return ok * 10 + 36;                               /* 4 * 10 + 36 */
}
