// C5: memmove z nakładającymi się obszarami, strchr na napisie const, freopen (NULL, jak fopen).
// expect-result: 1108
#include <stdio.h>
#include <string.h>

uchar buf[16] = {1, 2, 3, 4, 5, 6, 7, 8, 0};

int main(void)
{
    const uchar *text = "key=value";
    const uchar *eq = strchr(text, '=');
    int total = 0;

    memmove(buf + 2, buf, 6);                 /* do przodu: 1 2 1 2 3 4 5 6 */
    total += buf[2] + buf[7];                 /* 1 + 6 */
    memmove(buf, buf + 3, 4);                 /* do tyłu: 2 3 4 5 */
    total += buf[0] + buf[3];                 /* 2 + 5 */
    total += eq - text;                       /* 3 */
    total += strchr(text, 'z') == 0 ? 1000 : 0;
    total += freopen("x.txt", "r", stdin) == 0 ? 90 : 0;
    return total + 1;
}
