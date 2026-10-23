// Pętla z licznikiem: alokator rejestrów (z80/8080) i IR z instrukcją skoku warunkowego.
// expect-result: 45
// expect-ir: br Ge
// expect-regalloc: func sum
int sum(int n)
{
    int s = 0;
    int i;
    for (i = 0; i < n; i++) {
        s += i;
    }
    return s;
}

int main(void)
{
    return sum(10);
}
