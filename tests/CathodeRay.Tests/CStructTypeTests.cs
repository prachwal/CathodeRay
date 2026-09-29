using CathodeRay.C;
using FluentAssertions;

namespace CathodeRay.Tests;

/// <summary>Plan 28 D: struct (układ, pola, wskaźniki, tablice, kopiowanie, inicjalizatory).</summary>
public sealed class CStructTypeTests
{
    private static int Run(string source)
    {
        var (cpu, _, _) = CCodegenTests.RunC(source);
        return (cpu.State.X * 256) + cpu.State.A;
    }

    [Fact]
    public void Local_Struct_Fields_Read_And_Write()
    {
        const string Source = """
            struct P { uchar a; int b; uchar *c; };
            int main() {
                struct P p;
                uchar x = 7;
                p.a = 3;
                p.b = 1000;
                p.c = &x;
                p.a += 4;
                p.b++;
                return p.a + p.b + *p.c + sizeof(struct P) + sizeof p;
            }
            """;
        Run(Source).Should().Be(7 + 1001 + 7 + 5 + 5);
    }

    [Fact]
    public void Pointer_To_Struct_Arrow_And_Address_Of()
    {
        const string Source = """
            struct S { uchar n; int v; };
            struct S g;
            void bump(struct S *s, int by) { s->n++; s->v += by; }
            int main() {
                struct S loc;
                loc.n = 1;
                loc.v = 100;
                g.n = 10;
                g.v = 200;
                bump(&loc, 5);
                bump(&g, 50);
                struct S *p = &g;
                p->n *= 2;
                return loc.n + loc.v + g.n + g.v + p->v;
            }
            """;
        Run(Source).Should().Be(2 + 105 + 22 + 250 + 250);
    }

    [Fact]
    public void Array_Of_Structs_Index_Pointer_Arithmetic_And_Address()
    {
        const string Source = """
            struct T { uchar id; uchar w; int total; };
            struct T tab[4];
            int main() {
                for (int i = 0; i < 4; i++) {
                    tab[i].id = i + 1;
                    tab[i].w = i * 2;
                    tab[i].total = 100 * (i + 1);
                }
                struct T *p = tab + 2;
                struct T *q = &tab[3];
                p++;
                int span = sizeof(tab) / sizeof(struct T);
                return p->id * 1000 + q->total + tab[1].w * 10 + span + (p == q) + (tab + 1)->id;
            }
            """;
        Run(Source).Should().Be((4 * 1000) + 400 + 20 + 4 + 1 + 2);
    }

    [Fact]
    public void Struct_Copy_Assignment_And_Initialization_From_Another()
    {
        const string Source = """
            struct V { uchar a; int b; uchar c[3]; };
            struct V src;
            struct V all[2];
            int main() {
                src.a = 9;
                src.b = 555;
                src.c[0] = 1; src.c[1] = 2; src.c[2] = 3;
                struct V dst;
                dst = src;
                struct V twin = src;
                all[1] = src;
                struct V *p = &all[0];
                *p = all[1];
                src.a = 0;
                return dst.a + dst.b + dst.c[2] + twin.c[1] + all[0].b + all[1].c[0] + src.a;
            }
            """;
        Run(Source).Should().Be(9 + 555 + 3 + 2 + 555 + 1);
    }

    [Fact]
    public void Nested_Structs_And_Array_Fields()
    {
        const string Source = """
            struct In { uchar x; uchar y; };
            struct Out { struct In a; struct In b[2]; int tab[3]; };
            int main() {
                struct Out o;
                o.a.x = 1; o.a.y = 2;
                o.b[0].x = 3; o.b[1].y = 4;
                o.tab[2] = 300;
                struct In *ip = &o.b[1];
                ip->x = 5;
                int *tp = o.tab;
                return o.a.x + o.a.y * 10 + o.b[0].x * 10 + o.b[1].y + o.b[1].x + tp[2] + sizeof(struct Out);
            }
            """;
        Run(Source).Should().Be(1 + 20 + 30 + 4 + 5 + 300 + 12);
    }

    [Fact]
    public void Linked_List_With_Self_Referencing_Struct()
    {
        const string Source = """
            struct Node { int v; struct Node *next; };
            struct Node pool[3];
            int sum(struct Node *n) {
                int s = 0;
                while (n) { s += n->v; n = n->next; }
                return s;
            }
            int main() {
                pool[0].v = 10; pool[0].next = &pool[1];
                pool[1].v = 20; pool[1].next = &pool[2];
                pool[2].v = 30; pool[2].next = 0;
                return sum(pool) + sum(&pool[1]) * 100;
            }
            """;
        Run(Source).Should().Be(60 + 5000);
    }

    [Fact]
    public void Typedef_Struct_Named_And_Anonymous()
    {
        const string Source = """
            typedef struct { uchar r; uchar g; } Color;
            typedef struct Pt { int x; int y; } Point;
            Point origin;
            int dist(Point *a, Point *b) { return (b->x - a->x) + (b->y - a->y); }
            int main() {
                Color c;
                c.r = 200; c.g = 55;
                Point a;
                a.x = 3; a.y = 4;
                origin.x = 10; origin.y = 20;
                struct Pt *pp = &a;
                return c.r + c.g + dist(&a, &origin) + pp->y + sizeof(Color) + sizeof(Point);
            }
            """;
        Run(Source).Should().Be(255 + 23 + 4 + 2 + 4);
    }

    [Fact]
    public void Struct_Initializers_Local_And_Global()
    {
        const string Source = """
            struct P { uchar a; int b; };
            struct Q { struct P p; uchar t[3]; uchar *name; };
            struct P g = {7, 700};
            struct P gs[2] = { {1, 10}, {2, 20} };
            struct Q gq = { {5, 50}, {1, 2, 3}, 0 };
            int main() {
                struct P l = {9, 900};
                struct P part = {4};
                struct Q lq = { {6, 60}, "hi", "yo" };
                struct P arr[] = { {1, 2}, {3, 4} };
                return g.a + g.b + gs[1].b + gs[0].a + gq.p.b + gq.t[2] + l.b + part.a + part.b + lq.p.a + lq.t[1] + lq.name[1] + arr[1].b + sizeof(arr);
            }
            """;
        Run(Source).Should().Be(7 + 700 + 20 + 1 + 50 + 3 + 900 + 4 + 0 + 6 + 105 + 111 + 4 + 6);
    }

    [Fact]
    public void Postfix_And_Compound_On_Struct_Fields_And_Elements()
    {
        const string Source = """
            struct C { uchar n; int t; };
            struct C c[2];
            int main() {
                struct C *p = c;
                uchar old = p->n++;
                p->n += 5;
                c[1].t = 100;
                c[1].t--;
                int i = 0;
                c[i++].t += 7;
                return old * 1000 + p->n * 10 + c[1].t + c[0].t + i * 10000;
            }
            """;
        Run(Source).Should().Be(0 + 60 + 99 + 7 + 10000);
    }

    [Fact]
    public void Struct_Errors_Are_Reported()
    {
        string header = "struct S { uchar a; }; ";
        FluentActions.Invoking(() => Run(header + "int main() { struct S s; return s.nope; }"))
            .Should().Throw<CTypeException>().WithMessage("*no field 'nope'*");
        FluentActions.Invoking(() => Run(header + "int main() { struct S s; struct S *p = &s; return p.a; }"))
            .Should().Throw<CTypeException>().WithMessage("*'.' needs a struct*");
        FluentActions.Invoking(() => Run(header + "int main() { struct S s; return s->a; }"))
            .Should().Throw<CTypeException>().WithMessage("*'->' needs a pointer*");
        FluentActions.Invoking(() => Run(header + "int f(struct S s) { return 0; } int main() { return 0; }"))
            .Should().Throw<CTypeException>().WithMessage("*by value*");
        FluentActions.Invoking(() => Run(header + "struct S f() { struct S s; return s; } int main() { return 0; }"))
            .Should().Throw<CTypeException>().WithMessage("*by value*");
        FluentActions.Invoking(() => Run(header + "int main() { struct S s; struct S t; return s + t; }"))
            .Should().Throw<CTypeException>().WithMessage("*needs values*");
        FluentActions.Invoking(() => Run("struct A { struct A inner; }; int main() { return 0; }"))
            .Should().Throw<CTypeException>().WithMessage("*contains itself*");
        FluentActions.Invoking(() => Run("int main() { struct Nope n; return 0; }"))
            .Should().Throw<CTypeException>().WithMessage("*unknown struct Nope*");
    }

    [Fact]
    public void Struct_Pointer_Passed_Across_Modules()
    {
        string dir = Directory.CreateTempSubdirectory("cathode-struct-").FullName;
        try
        {
            string hdr = "struct R { uchar w; uchar h; int area; };\n";
            string a = Path.Combine(dir, "a.c");
            string b = Path.Combine(dir, "b.c");
            File.WriteAllText(a, hdr + "void fill(struct R *r) { r->area = r->w * r->h; }\n");
            File.WriteAllText(b, hdr + "void fill(struct R *r);\nint main() { struct R r; r.w = 6; r.h = 7; fill(&r); return r.area; }\n");
            var err = new StringWriter();
            string bin = Path.Combine(dir, "p.bin");
            int exit = CathodeRay.Cli.CliApp.CreateRoot().Parse(["cc", a, b, "-o", bin])
                .Invoke(new System.CommandLine.InvocationConfiguration { Output = new StringWriter(), Error = err });
            exit.Should().Be(0, err.ToString());
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void Global_Aggregates_With_Addresses_Inside()
    {
        const string Source = """
            struct Entry { uchar id; uchar *name; int *slot; };
            int nums[3] = {11, 22, 33};
            uchar *names[] = {"ab", "cde", "f"};
            struct Entry table[2] = { {1, "xy", &nums[1]}, {2, "q", nums + 2} };
            struct Entry gs;
            uchar *later[2] = { &table[1].id, &gs.id };
            int main() {
                return *table[0].slot + names[1][2] + names[2][0] + table[0].name[1] + *table[1].slot + table[1].id + later[0][0] + sizeof(names);
            }
            """;
        Run(Source).Should().Be('e' + 'f' + 'y' + 22 + 33 + 2 + 2 + 6);
    }

    [Fact]
    public void Recursive_Function_Keeps_Local_Array_And_Struct_Per_Frame()
    {
        const string Source = """
            struct P { uchar a; int b; };
            int depth(int n) {
                uchar tag[3] = {1, 2, 3};
                struct P p = {n, n * 10};
                int below = 0;
                if (n > 0) below = depth(n - 1);
                tag[0] += n;
                return below + tag[0] + tag[2] + p.a + p.b;
            }
            int main() { return depth(3); }
            """;
        Run(Source).Should().Be(88);
    }

    [Fact]
    public void Recursive_Function_With_Oversized_Local_Is_Rejected()
    {
        FluentActions.Invoking(() => Run("int f(int n) { uchar big[100]; big[0] = n; if (n > 0) f(n - 1); return big[0]; } int main() { return f(2); }"))
            .Should().Throw<CCodegenException>().WithMessage("*recursive function 'f'*100 B*");
    }

    [Fact]
    public void Large_Struct_Copy_And_Local_Initializer_Beyond_255_Bytes()
    {
        const string Source = """
            struct Big { uchar head; uchar body[298]; uchar tail; };
            struct Big a;
            struct Big b;
            int main() {
                for (int i = 0; i < 298; i++) a.body[i] = i;
                a.head = 1;
                a.tail = 7;
                b = a;
                uchar loc[300] = {5};
                return sizeof(struct Big) + b.head + b.body[255] + b.body[297] + b.tail + loc[0] + loc[299] + loc[256];
            }
            """;
        Run(Source).Should().Be(1 + 255 + (297 & 255) + 7 + 5 + 0 + 0 + 300);
    }
}
