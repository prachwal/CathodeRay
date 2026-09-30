#!/usr/bin/env python3
"""Test różnicowy funkcji liściowych z parametrami (plan 33): losowe programy C, wynik z gcc kontra nasz kompilator.

  python3 tools/leaf_fuzz.py gen 120 [KATALOG]     # generuje pNN_ours.c / pNN_gcc.c i gcc.txt (wynik referencyjny z gcc)
  LEAF_FUZZ_DIR=KATALOG dotnet test --nologo --filter LeafFuzzTests   # kompiluje pNN_ours.c na wszystkich celach i porównuje z gcc.txt

Domyślny katalog: /tmp/leaf-fuzz. Wymaga gcc. Liście g0..g2 mają 1-3 parametry szerokości 1/2/4 (U8, I16, U16, I32), zmieniają je
(także w pętlach), a main woła je wielokrotnie ze stałymi i własnymi zmiennymi (które potem też wchodzą do sumy kontrolnej).
Tylko działania, które dają ten sam wynik mod 2^k w gcc i w naszym C (16-bitowy int): + - & | ^, jednoargumentowe - ~,
stałe I32 także graniczne (0x7FFFFFFF, 0x80000000, 0xFFFFFFFF, przeniesienia przez bajty i połówki), porównania zmiennej ze stałą
(także graniczną) albo ze zmienną tego samego typu,
operandy rzutowane na typ wyniku (w mini-C działanie na dwóch uchar jest 8-bitowe, docs/minic.md).
"""
import os
import random
import subprocess
import sys

TYPES = ["U8", "I16", "U16", "I32"]
WIDTH = {"U8": 1, "I16": 1, "U16": 1, "I32": 2}


def const(r, t):
    if t == "U8":
        return str(r.randint(0, 255))
    if t == "I32":
        if r.random() < 0.3:
            # wartości graniczne long i przeniesienia przez granice bajtów i połówek
            return r.choice(["2147483647L", "(-2147483647L - 1)", "-1L", "255L", "256L", "65535L", "65536L", "16777215L", "16777216L", "-65536L"])
        return f"{r.choice([r.randint(-99999, 99999), r.randint(0, 9)])}L"
    if r.random() < 0.25:
        # wartości graniczne słowa 16-bitowego (przeniesienie/pożyczka i przepełnienie ze znakiem)
        return r.choice(["32767", "-32767", "(-32767 - 1)", "-1", "0", "1"] if t == "I16" else ["65535", "32768", "32767", "0", "1"])
    return str(r.randint(-300, 30000) if t == "I16" else r.randint(0, 32767))


def function(r, name, static):
    params, halves = [], 0
    for i in range(r.randint(1, 3)):
        t = r.choice(TYPES)
        if halves + WIDTH[t] > 6:
            break
        halves += WIDTH[t]
        params.append((f"p{i}", t))
    locals_ = [("l0", r.choice(TYPES)), ("l1", r.choice(TYPES))]
    ret = r.choice(TYPES)
    vs = params + locals_
    types = dict(vs)
    lines = []

    def operand(target, pool=None, cast=True):
        names = [n for n, t in (pool or vs)]
        v = r.choice(names + ["K"])
        text = const(r, target) if v == "K" else v
        # mini-C: uchar op uchar zostaje 8-bitowe, więc lewy operand ma typ wyniku (rzutowanie)
        return text if target == "U8" or not cast else f"({target}){text}"

    def assign(pad, v):
        t = types[v]
        lines.append(f"{pad}{v} = {operand(t)} {r.choice(['+', '-', '&', '|', '^'])} {operand(t, cast=False)};")

    def block(depth, indent, locked):
        pad = " " * indent
        for _ in range(r.randint(2, 4)):
            k, v = r.random(), r.choice([n for n, _ in vs if n not in locked])
            if k < 0.37:
                assign(pad, v)
            elif k < 0.45:
                lines.append(f"{pad}{v} = {r.choice(['-', '~'])}({operand(types[v])});")
            elif k < 0.65 and depth < 2:
                lines.append(f"{pad}{v} = {v} & 7;")
                lines.append(f"{pad}while ({v} > 0) {{")
                block(depth + 1, indent + 2, locked | {v})
                lines.append(f"{pad}  {v} = {v} - 1;")
                lines.append(f"{pad}}}")
            elif k < 0.85 and depth < 2:
                c = r.random()
                if c < 0.3:
                    cond = f"{v} > {r.randint(0, 100)}"
                elif c < 0.5:
                    cond = f"({v} & 3) == {r.randint(0, 3)}"
                else:
                    # porównanie ze znakiem/bez znaku z inną zmienną tego samego typu albo ze stałą (także graniczną)
                    same = [n for n, t in vs if t == types[v]]
                    rhs = r.choice(same) if c < 0.75 else const(r, types[v])
                    cond = f"{v} {r.choice(['<', '<=', '>', '>='])} {rhs}"
                lines.append(f"{pad}if ({cond}) {{")
                block(depth + 1, indent + 2, locked)
                lines.append(f"{pad}}}")
            else:
                lines.append(f"{pad}{v} = {v} + {r.randint(1, 9)};")

    lines.append(f"{'static ' if static else ''}{ret} {name}({', '.join(f'{t} {n}' for n, t in params)}) {{")
    for n, t in locals_:
        lines.append(f"  {t} {n};")
    for n, t in locals_:
        lines.append(f"  {n} = {operand(t, params)};")
    block(0, 2, set())
    lines.append(f"  return {operand(ret)} {r.choice(['+', '-', '^'])} {operand(ret, cast=False)};")
    lines.append("}")
    return "\n".join(lines), params


def program(seed):
    r = random.Random(seed)
    functions, body = [], []
    for f in range(3):
        text, params = function(r, f"g{f}", static=r.random() < 0.3)
        functions.append(text)
        functions.append("")
        for _ in range(r.randint(1, 3)):
            args = []
            for _, t in params:
                if r.random() < 0.4:
                    body.append(f"  m{t} = {const(r, t)};")
                    args.append(f"m{t}")
                else:
                    args.append(const(r, t))
            body.append(f"  h = h * 31 + (U16)g{f}({', '.join(args)});")
            body.append("  h = h ^ (U16)mU8 ^ (U16)mI16 ^ (U16)mU16 ^ (U16)mI32;")
    main = ["int main() {", "  U16 h; U8 mU8; I16 mI16; U16 mU16; I32 mI32;", "  h = 0; mU8 = 1; mI16 = 2; mU16 = 3; mI32 = 4L;", *body, "  return h & 32767;", "}"]
    return "\n".join(functions + main)


def generate(count, out):
    os.makedirs(out, exist_ok=True)
    results = []
    for seed in range(count):
        body = program(seed)
        with open(os.path.join(out, f"p{seed}_ours.c"), "w") as f:
            f.write("typedef uchar U8; typedef int I16; typedef uint U16; typedef long I32;\n" + body + "\n")
        gcc_source = os.path.join(out, f"p{seed}_gcc.c")
        with open(gcc_source, "w") as f:
            f.write("#include <stdio.h>\ntypedef unsigned char U8; typedef short I16; typedef unsigned short U16; typedef int I32;\n"
                    + body.replace("int main() {", "int main_() {") + '\nint main() { printf("%d\\n", main_()); return 0; }\n')
        binary = os.path.join(out, f"g{seed}")
        if subprocess.run(["gcc", "-w", "-O0", "-o", binary, gcc_source], capture_output=True).returncode:
            continue
        results.append(f"p{seed} {subprocess.run([binary], capture_output=True, text=True, timeout=5).stdout.strip()}")
    with open(os.path.join(out, "gcc.txt"), "w") as f:
        f.write("\n".join(results) + "\n")
    print(f"{len(results)} programów w {out}")


if __name__ == "__main__":
    if len(sys.argv) < 3 or sys.argv[1] != "gen":
        sys.exit(__doc__)
    generate(int(sys.argv[2]), sys.argv[3] if len(sys.argv) > 3 else "/tmp/leaf-fuzz")
