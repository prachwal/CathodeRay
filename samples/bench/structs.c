struct P { int x; int y; uchar tag; };
int area(struct P *p, struct P *q) { return (q->x - p->x) + (q->y - p->y) + q->tag; }
void move(struct P *p, int dx) { p->x = p->x + dx; p->tag++; }
