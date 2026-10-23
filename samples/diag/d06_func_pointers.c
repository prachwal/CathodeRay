// Tablica wskaźników do funkcji: wołanie pośrednie.
// expect-result: 59
int add(int a, int b) { return a + b; }
int sub(int a, int b) { return a - b; }
int mul(int a, int b) { return a * b; }

int (*ops[3])(int, int) = {add, sub, mul};

int main(void)
{
    int acc = 7;
    uchar i;
    for (i = 0; i < 3; i++) {
        acc = ops[i](acc, 6);
    }
    return acc + 17;
}
