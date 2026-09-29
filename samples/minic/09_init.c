// 09_init.c — inicjalizatory tablic, napisy, globalny wskaźnik, sizeof, enum, ++/+=.
// tab = 1+2+3+4 = 10; msg[1] = 'e' = 101; wide[1] / 100 = 20; sizeof(wide) = 4; loc[1] - 'a' = 1.
// Oczekiwane: A = 136.
enum { N = 4 };
uchar tab[N] = {1, 2, 3, 4};
uchar *msg = "hey";
int wide[] = {1000, 2000};

int main() {
    uchar loc[] = "ab";
    int total = 0;
    for (int i = 0; i < N; i++) total += tab[i];
    total += msg[1];
    total += wide[1] / 100;
    total += sizeof(wide);
    total += loc[1] - 'a';
    return total;
}
