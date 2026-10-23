// Tablica struktur i arytmetyka wskaźników.
// expect-result: 114
struct item {
    uchar id;
    uchar weight;
    int price;
};

struct item items[4] = {
    {1, 2, 10},
    {2, 3, 20},
    {3, 4, 30},
    {4, 5, 40},
};

int total(struct item *p, uchar n)
{
    int sum = 0;
    while (n--) {
        sum += p->price + p->weight;
        p++;
    }
    return sum;
}

int main(void)
{
    return total(items, 4);
}
