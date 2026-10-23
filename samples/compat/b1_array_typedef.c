// B1: typedef typu tablicowego (typedef int jmp_buf[N]): zmienna globalna i lokalna, parametr (rozpad na wskaźnik),
// sizeof, tablica dwuwymiarowa i kilka deklaratorów w jednej deklaracji.
// expect-result: 234
typedef int vec4[4];
typedef uchar grid[2][3];

vec4 global_vec;
grid cells;

int sum(vec4 v)
{
    return v[0] + v[1] + v[2] + v[3];
}

void fill(vec4 v, int base)
{
    uchar i;
    for (i = 0; i < 4; i++) {
        v[i] = base + i;
    }
}

int main(void)
{
    vec4 local, other;
    uchar r;
    uchar c;
    int total;

    fill(global_vec, 10);
    fill(local, 20);
    other[0] = 1;
    other[3] = 2;
    for (r = 0; r < 2; r++) {
        for (c = 0; c < 3; c++) {
            cells[r][c] = r * 3 + c;
        }
    }

    total = sum(global_vec) + sum(local);
    total += cells[1][2] + other[0] + other[3];
    return total + sizeof(vec4) + sizeof(grid) + sizeof(local) * 10;
}
