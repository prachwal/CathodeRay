#!/usr/bin/env python3
"""Test różnicowy rekurencji: losowe programy C (rekurencja, pętle, goto w przód, lokalne żywe przez wołanie), wynik z gcc kontra nasz kompilator.

  python3 tools/recursion_fuzz.py gen 120 [KATALOG]     # generuje pNN_ours.c / pNN_gcc.c i gcc.txt (wynik referencyjny z gcc)
  RECURSION_FUZZ_DIR=KATALOG dotnet test --nologo --filter RecursionFuzzTests   # kompiluje pNN_ours.c na stub, 6502, z80 i porównuje z gcc.txt

Domyślny katalog: /tmp/recursion-fuzz. Wymaga gcc. Typ I16 to `int` naszego kompilatora i `short` w gcc; funkcje mają lokalne a, b, c, pętle (zmienne i, j),
wołania f(n - 1) w warunku n > 0 i skoki w przód do etykiet na końcu funkcji.
"""
import os
import random
import subprocess
import sys


def program(seed):
    r = random.Random(seed)
    lines, labels, counter = [], [], [0]

    def expr(vs):
        return f"({r.choice(vs)} {r.choice(['+', '-', '*', '^', '&', '|'])} {r.choice(vs + ['n', '3', '7'])})"

    def block(depth, indent, vs):
        for _ in range(r.randint(2, 5)):
            k, v, pad = r.random(), r.choice(vs), ' ' * indent
            if k < 0.30:
                lines.append(f"{pad}{v} = {expr(vs)};")
            elif k < 0.50:
                lines.append(f"{pad}if (n > 0) {v} = f(n - 1) {r.choice(['+', '^', '-'])} {r.choice(vs)};")
            elif k < 0.62 and depth < 2:
                iv = "ij"[depth]
                lines.append(f"{pad}for ({iv} = 0; {iv} < {r.randint(1, 3)}; {iv}++) {{")
                block(depth + 1, indent + 2, vs)
                lines.append(f"{pad}}}")
            elif k < 0.75 and depth < 2:
                lines.append(f"{pad}if ({expr(vs)} & 1) {{")
                block(depth + 1, indent + 2, vs)
                lines.append(f"{pad}}}")
            elif k < 0.87:
                counter[0] += 1
                label = f"L{counter[0]}"
                lines.append(f"{pad}if (({expr(vs)} & 2) != 0) goto {label};")
                labels.append(label)
            else:
                lines.append(f"{pad}{v} = {v} + {r.randint(1, 9)};")

    vs = ['a', 'b', 'c']
    lines += ["I16 f(I16 n) {", "  I16 a; I16 b; I16 c; I16 i; I16 j;", "  a = n * 3; b = n + 1; c = n ^ 5;"]
    block(0, 2, vs)
    for label in labels:
        lines.append(f"  {label}: {r.choice(vs)} = {r.choice(vs)} + {r.randint(1, 5)};")
    lines += ["  return (a + b * 2 + c * 3) & 32767;", "}"]
    return "\n".join(lines)


def generate(count, out):
    os.makedirs(out, exist_ok=True)
    results = []
    for seed in range(count):
        body = program(seed)
        with open(os.path.join(out, f"p{seed}_ours.c"), "w") as f:
            f.write("typedef int I16;\n" + body + "\nint main() { return f(3) & 32767; }\n")
        gcc_source = os.path.join(out, f"p{seed}_gcc.c")
        with open(gcc_source, "w") as f:
            f.write('#include <stdio.h>\ntypedef short I16;\n' + body + '\nint main() { printf("%d\\n", f(3) & 32767); return 0; }\n')
        binary = os.path.join(out, f"g{seed}")
        if subprocess.run(["gcc", "-w", "-O0", "-o", binary, gcc_source], capture_output=True).returncode:
            continue
        try:
            value = subprocess.run([binary], capture_output=True, text=True, timeout=5).stdout.strip()
        except subprocess.TimeoutExpired:
            continue
        results.append(f"p{seed} {value}")
    with open(os.path.join(out, "gcc.txt"), "w") as f:
        f.write("\n".join(results) + "\n")
    print(f"{len(results)} programów w {out}")


if __name__ == "__main__":
    if len(sys.argv) < 3 or sys.argv[1] != "gen":
        sys.exit(__doc__)
    generate(int(sys.argv[2]), sys.argv[3] if len(sys.argv) > 3 else "/tmp/recursion-fuzz")
