// Struktury przez wartość: argument, wynik, zagnieżdżenie i rekurencja. Wynik: 936.
struct Vec { int x; int y; };
struct Box { struct Vec lo; struct Vec hi; uchar tag; };

struct Vec make(int x, int y) {
    struct Vec v;
    v.x = x;
    v.y = y;
    return v;
}

struct Vec add(struct Vec a, struct Vec b) {
    a.x = a.x + b.x;             // zmiana kopii nie rusza argumentu wołającego
    a.y = a.y + b.y;
    return a;
}

int area(struct Box b) {
    return (b.hi.x - b.lo.x) * (b.hi.y - b.lo.y);
}

struct Vec scale(struct Vec v, int n) {
    if (n == 0) {
        return make(0, 0);
    }
    return add(v, scale(v, n - 1));    // v * n rekurencyjnie
}

int main() {
    struct Vec p = make(3, 4);
    struct Vec q = add(p, make(10, 20));
    struct Box box;
    box.lo = make(1, 2);
    box.hi = add(box.lo, make(9, 8));
    box.tag = 7;
    struct Vec s = scale(p, 5);
    p = make(0, 0);
    return q.x * 10 + q.y + area(box) * 10 + s.x + s.y + add(q, q).x - 6 + box.tag + p.x;
}
