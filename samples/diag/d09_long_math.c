// Arytmetyka 32-bitowa (long): mnożenie, dzielenie i przesunięcia przez helpery biblioteki wykonawczej.
// expect-result: 14375
// expect-ir: Mul
long scale(long x)
{
    return (x * 7) / 3 + (x >> 4);
}

int main(void)
{
    long v = scale(6000L);
    return (int)(v % 65536L);
}
