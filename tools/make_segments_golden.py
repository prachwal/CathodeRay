#!/usr/bin/env python3
"""Generuje wzorce segmentów w tests/CathodeRay.Tests/Segments/.

Dla każdego CPU pisze main.s (nasza składnia: .segment/.code/.data/.bss)
+ ref.s (składnia referencji) + map.txt, składa referencją (ca65+ld65 z .cfg,
z80asm z SECTION + sklejanie CODE/DATA) i zapisuje expected.bin.
Weryfikuje SegmentsTests.cs (odkrywanie po katalogu).
"""

import os
import pathlib
import subprocess
import sys

REPO = pathlib.Path(__file__).resolve().parent.parent
OUT = REPO / "tests" / "CathodeRay.Tests" / "Segments"

CASES = {
    # nazwa: (cpu, mapa, nasze źródło, źródło referencji, ref)
    "6502": (
        "6502",
        0x0200,
        '.segment "CODE"\nstart: lda #1\njmp done\n.segment "DATA"\nval: .byte 9\ntable: .word start\n.bss\nbuf: .byte 0\n.segment "CODE"\ndone: lda val\nrts\n',
        '.segment "CODE"\nstart: lda #1\njmp done\n.segment "DATA"\nval: .byte 9\ntable: .word start\n.segment "BSS"\nbuf: .byte 0\n.segment "CODE"\ndone: lda val\nrts\n',
        "ca65-6502",
    ),
    "6502x": (
        "6502x",
        0x0200,
        '.segment "CODE"\nlax $12\n.segment "DATA"\nval: .byte 9\n.bss\nbuf: .byte 0\n.segment "CODE"\nrts\n',
        '.segment "CODE"\nlax $12\n.segment "DATA"\nval: .byte 9\n.segment "BSS"\nbuf: .byte 0\n.segment "CODE"\nrts\n',
        "ca65-6502X",
    ),
    "65c02": (
        "65c02",
        0x0400,
        '.segment "CODE"\nstz $20\n.segment "DATA"\nval: .byte 9\n.bss\nbuf: .byte 0\n.segment "CODE"\nrts\n',
        '.segment "CODE"\nstz $20\n.segment "DATA"\nval: .byte 9\n.segment "BSS"\nbuf: .byte 0\n.segment "CODE"\nrts\n',
        "ca65-65C02",
    ),
    "z80": (
        "z80",
        {"CODE": 0x8000, "DATA": 0x8100, "BSS": 0x8200},
        'SEGMENT "CODE"\nstart: LD A,1\nJP done\nSEGMENT "DATA"\nval: DEFB 9\nBSS\nbuf: DEFS 1\nSEGMENT "CODE"\ndone: LD A,(val)\nRET\n',
        "SECTION CODE\nORG 8000H\nstart: LD A,1\nJP done\nSECTION DATA\nORG 8100H\nval: DEFB 9\nSECTION BSS\nORG 8200H\nbuf: DEFS 1\nSECTION CODE\ndone: LD A,(val)\nRET\n",
        "z80-strict",
    ),
    "z80u": (
        "z80u",
        {"CODE": 0x8000, "DATA": 0x8100, "BSS": 0x8200},
        'SEGMENT "CODE"\nLD A,IXH\nSEGMENT "DATA"\nval: DEFB 9\nBSS\nbuf: DEFS 1\nSEGMENT "CODE"\nRET\n',
        "SECTION CODE\nORG 8000H\nLD A,IXH\nSECTION DATA\nORG 8100H\nval: DEFB 9\nSECTION BSS\nORG 8200H\nbuf: DEFS 1\nSECTION CODE\nRET\n",
        "z80",
    ),
    "8080": (
        "8080",
        {"CODE": 0x0100, "DATA": 0x0200, "BSS": 0x0300},
        'SEGMENT "CODE"\nstart: MVI A,1\nJMP done\nSEGMENT "DATA"\nval: DB 9\nBSS\nbuf: DS 1\nSEGMENT "CODE"\ndone: LDA val\nHLT\n',
        "SECTION CODE\nORG 100H\nstart: MVI A,1\nJMP done\nSECTION DATA\nORG 200H\nval: DB 9\nSECTION BSS\nORG 300H\nbuf: DS 1\nSECTION CODE\ndone: LDA val\nHLT\n",
        "8080",
    ),
}

CFG = (
    "MEMORY { RAM: start=$BASE, size=$1000, file=%O, fill=yes; }\n"
    "SEGMENTS { CODE: load=RAM, type=rw; DATA: load=RAM, type=rw; BSS: load=RAM, type=bss, define=yes; }\n"
)


def ref_ca65(work, cpu, base, map_addrs):
    (work / "ref.s").write_text(CASES[cur][3])
    r = subprocess.run(
        ["ca65", "--cpu", cpu, "ref.s", "-o", "p.o"],
        cwd=work,
        capture_output=True,
        text=True,
    )
    if r.returncode != 0:
        print(r.stderr[-1000:])
        return None
    (work / "raw.cfg").write_text(CFG.replace("$BASE", f"${base:04X}"))
    r = subprocess.run(
        ["ld65", "-C", "raw.cfg", "p.o", "-o", "ref.bin", "-m", "link.map"],
        cwd=work,
        capture_output=True,
        text=True,
    )
    if r.returncode != 0:
        print(r.stderr[-1000:])
        return None
    in_segments = False
    for line in (work / "link.map").read_text().splitlines():
        if line.startswith("Segment list:"):
            in_segments = True
            continue
        if in_segments and not line.strip():
            break
        parts = line.split()
        if in_segments and len(parts) >= 4 and parts[0] in map_addrs:
            try:
                map_addrs[parts[0]] = int(parts[1], 16)
            except ValueError:
                pass
    binary = (work / "ref.bin").read_bytes().rstrip(b"\x00")
    assert len(binary) > 0 and binary[-1] != 0
    return binary


def ref_z80(work, mode, addrs):
    (work / "ref.s").write_text(CASES[cur][3])
    env = dict(
        os.environ,
        PATH="/opt/z88dk-2.4/bin:" + os.environ["PATH"],
        ZCCCFG="/opt/z88dk-2.4/lib/config",
    )
    r = subprocess.run(
        ["z80asm", f"-m{mode}", "-b", "ref.s"],
        cwd=work,
        capture_output=True,
        text=True,
        env=env,
    )
    if r.returncode != 0:
        print(r.stderr[-1000:])
        return None
    code = (work / "ref_CODE.bin").read_bytes()
    data = (work / "ref_DATA.bin").read_bytes()
    gap = addrs["DATA"] - (addrs["CODE"] + len(code))
    assert gap >= 0, "DATA przed końcem CODE"
    return code + bytes(gap) + data


def main() -> None:
    global cur
    for cur, (cpu, base_or_map, ours, refsrc, ref) in CASES.items():
        work = OUT / cur
        work.mkdir(parents=True, exist_ok=True)
        (work / "main.s").write_text(ours)
        if ref.startswith("ca65"):
            map_addrs = {"CODE": 0, "DATA": 0, "BSS": 0}
            binary = ref_ca65(work, ref.split("-")[1], base_or_map, map_addrs)
        else:
            map_addrs = dict(base_or_map)
            binary = ref_z80(
                work,
                {"z80-strict": "z80_strict", "z80": "z80", "8080": "8080"}[ref],
                map_addrs,
            )
        (work / "map.txt").write_text(
            "".join(f"{n}@${a:04X}\n" for n, a in map_addrs.items())
        )
        if binary is None:
            raise SystemExit(f"referencja odrzuca Segments/{cur}")
        binary = binary.rstrip(b"\x00")
        assert len(binary) > 0 and binary[-1] != 0
        (work / "expected.bin").write_bytes(binary)
        for f in work.glob("*"):
            if f.is_file() and f.name not in ("main.s", "map.txt", "expected.bin"):
                f.unlink()
        print(f"{cur}: expected.bin ({len(binary)} B)")


if __name__ == "__main__":
    main()
