// Wynik ujemny: `cathode run` pokazuje go ze znakiem i szesnastkowo.
// expect-result: -300
// expect-result-text: result: -300 (0xFED4)
int main(void)
{
    int a = 100;
    int b = 400;
    return a - b;
}
