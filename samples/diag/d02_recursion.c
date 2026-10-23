// Rekurencja: kompilator ostrzega o głębokości stosu, a wynik trzeba policzyć poprawnie.
// expect-result: 55
// expect-warning: is recursive
int fib(int n)
{
    if (n < 2) {
        return n;
    }
    return fib(n - 1) + fib(n - 2);
}

int main(void)
{
    return fib(10);
}
