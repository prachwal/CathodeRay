// B1: typedef tablicowy z własnym wymiarem deklaratora (row m[3] => int[3][4]), parametr row r[] i unsized z inicjalizatorem.
// expect-result: 593
typedef int row[4];

row grid[3];
row seeded[] = {{1, 2, 3, 4}, {5, 6, 7, 8}};

int second_row_sum(row r[])
{
    return r[1][0] + r[1][1] + r[1][2] + r[1][3];
}

int main(void)
{
    uchar i;
    uchar j;
    int total = 0;

    for (i = 0; i < 3; i++) {
        for (j = 0; j < 4; j++) {
            grid[i][j] = i * 10 + j;
        }
    }
    total += sizeof(grid) * 10;                 /* 3 * 4 * 2 = 24 -> 240 */
    total += sizeof(seeded);                    /* 2 * 4 * 2 = 16 */
    total += second_row_sum(grid);              /* 10 + 11 + 12 + 13 = 46 */
    total += second_row_sum(seeded) * 10;       /* 5 + 6 + 7 + 8 = 26 -> 260 */
    total += grid[2][3] + seeded[1][3];         /* 23 + 8 */
    return total;
}
