// Tablice wielowymiarowe: m[i][j], sizeof, inicjalizatory, wskaźnik do wiersza jako parametr. Wynik: 591.
int table[3][4] = {{1, 2, 3, 4}, {5, 6, 7, 8}, {9, 10, 11, 12}};

int sum_rows(int (*rows)[4], int count) {
    int total = 0;
    int i = 0;
    while (i < count) {
        int j = 0;
        while (j < 4) {
            total = total + rows[i][j];
            j++;
        }
        i++;
    }
    return total;
}

int trace(int m[][4]) {
    return m[0][0] + m[1][1] + m[2][2];
}

int main() {
    int local[2][3] = {{10, 20, 30}, {40, 50, 60}};
    uchar grid[3][5];
    int i;
    int j;
    for (i = 0; i < 3; i++) {
        for (j = 0; j < 5; j++) {
            grid[i][j] = (uchar)(i * 5 + j);
        }
    }
    int *row = local[1];
    int (*p)[3] = local;
    table[2][3] = table[2][3] + 100;
    return sum_rows(table, 3) + trace(table) * 10 + local[1][2] + row[1] + p[0][2] + grid[2][4] + sizeof(table) + sizeof(table[0]) + sizeof(grid) + (*(p + 1))[0] - 8;
}
