#!/usr/bin/env python3
"""Analiza najczęstszych par i trójek instrukcji w listingach 6502 z samples/bench.

Użycie: python3 tools/hotspots.py [--write]
  Bez --write: drukuje tabelę; z --write zapisuje docs/hotspots.md
"""
import glob
import os
import re
import subprocess
import sys
import tempfile
from collections import Counter

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
DLL = os.path.join(ROOT, "src/CathodeRay.Cli/bin/Debug/net10.0/cathode.dll")


def normalize_operand(op):
    """Normalizuje operand: #… → #I, z:… → ZP, reszta → M."""
    if op.startswith('#'):
        return '#I'
    if ':' in op and op.startswith(('z:', '$')):
        return 'ZP'
    if op:
        return 'M'
    return op


def extract_instructions(listing_path, bench_name):
    """Wyodrębnia znormalizowane instrukcje z listingu dla danego benchmarku."""
    instructions = []
    try:
        with open(listing_path, 'r', encoding='utf-8', errors='replace') as f:
            for line in f:
                # Pomijamy linię segmentu (**** SEGMENT ***)
                if line.startswith('*****'):
                    continue

                # Format: ADDRESS  BYTES  LOCATION  SOURCE
                # Szukamy linii z bench_name w LOCATION
                if bench_name not in line:
                    continue

                # Wyodrębniamy SOURCE - ostatnia część po LOCATION
                # Struktura: "AAAA  HH HH HH HH  file:line  source"
                # Lub:       "AAAA  HH HH HH HH  source" (bez pliku)
                # Szukamy części po ":" w LOCATION
                match = re.search(r'(\d+)\s+([A-Fa-f0-9 ]*?)\s+(\S+):(\d+)\s+(.+)$', line)
                if not match:
                    continue

                source = match.group(5).strip()
                if not source:
                    continue

                # Wyodrębniamy mnemonik i operand
                # Typowy format: "lda #$FF" lub "sta $1234" lub "jmp"
                parts = source.split()
                if len(parts) < 1:
                    continue

                mnemonic = parts[0].lower()
                operand = ' '.join(parts[1:]) if len(parts) > 1 else ''

                # Filtrujemy tylko prawidłowe instrukcje 6502
                valid_mnemonics = {
                    'lda', 'sta', 'ldy', 'sty', 'ldx', 'stx',
                    'adc', 'sbc', 'and', 'ora', 'eor', 'cmp', 'cpx', 'cpy',
                    'iny', 'inx', 'dey', 'dex', 'tay', 'txa', 'tya', 'tax',
                    'jmp', 'jsr', 'rts', 'rti', 'brk',
                    'inc', 'dec', 'asl', 'lsr', 'rol', 'ror',
                    'bne', 'beq', 'bcs', 'bcc', 'bvs', 'bvc', 'bmi', 'bpl',
                    'php', 'plp', 'pha', 'pla', 'phs', 'pls',
                    'clc', 'sec', 'cli', 'sei', 'clv', 'cld', 'sed',
                    'nop'
                }

                if mnemonic not in valid_mnemonics:
                    continue

                normalized_op = normalize_operand(operand)
                instr = f"{mnemonic} {normalized_op}".rstrip()
                instructions.append(instr)
    except Exception as e:
        print(f"Błąd przy parsowaniu {listing_path}: {e}", file=sys.stderr)

    return instructions


def compile_bench(bench_path, temp_dir):
    """Kompiluje plik benchu i zwraca ścieżkę do listingu."""
    listing_path = os.path.join(temp_dir, 'x.lst')
    bin_path = os.path.join(temp_dir, 'x.bin')
    prelude_path = os.path.join(temp_dir, 'm.c')

    # Tworzymy preludium (pusta main)
    with open(prelude_path, 'w') as f:
        f.write('int main(){return 0;}')

    # Kompilujemy
    cmd = [
        'dotnet', DLL, 'cc',
        bench_path,
        prelude_path,
        '--nostdlib',
        '-o', bin_path,
        '-l', listing_path,
        '--cpu', '6502'
    ]

    result = subprocess.run(cmd, capture_output=True, text=True)
    if result.returncode != 0:
        print(f"Błąd kompilacji {bench_path}: {result.stderr[:200]}", file=sys.stderr)
        return None

    return listing_path if os.path.exists(listing_path) else None


def main():
    write_output = '--write' in sys.argv

    # Zbieramy instrukcje ze wszystkich benchów
    all_instructions = []

    with tempfile.TemporaryDirectory(prefix='hotspot-') as tmpdir:
        bench_files = sorted(glob.glob(os.path.join(ROOT, 'samples/bench/*.c')))
        for bench_path in bench_files:
            bench_name = os.path.basename(bench_path)[:-2]  # Usuwamy .c
            listing_path = compile_bench(bench_path, tmpdir)
            if listing_path is None:
                continue

            instructions = extract_instructions(listing_path, bench_name)
            all_instructions.extend(instructions)

    # Liczymy pary i trójki
    pairs = Counter()
    triples = Counter()

    for i in range(len(all_instructions) - 1):
        pair = f"{all_instructions[i]} ; {all_instructions[i + 1]}"
        pairs[pair] += 1

        if i + 2 < len(all_instructions):
            triple = f"{all_instructions[i]} ; {all_instructions[i + 1]} ; {all_instructions[i + 2]}"
            triples[triple] += 1

    # Formatujemy wynik
    lines = []
    lines.append('# Najczęstsze pary i trójki instrukcji 6502')
    lines.append('')
    lines.append(f'Całkowita liczba instrukcji: {len(all_instructions)}')
    lines.append('')
    lines.append('## Top 15 par instrukcji')
    lines.append('')
    lines.append('| Para | Liczba |')
    lines.append('| --- | ---: |')

    for pair, count in pairs.most_common(15):
        lines.append(f'| `{pair}` | {count} |')

    lines.append('')
    lines.append('## Top 10 trójek instrukcji')
    lines.append('')
    lines.append('| Trójka | Liczba |')
    lines.append('| --- | ---: |')

    for triple, count in triples.most_common(10):
        lines.append(f'| `{triple}` | {count} |')

    text = '\n'.join(lines) + '\n'
    print(text)

    if write_output:
        output_path = os.path.join(ROOT, 'docs/hotspots.md')
        with open(output_path, 'w') as f:
            f.write(text)
        print(f"Zapisano do {output_path}", file=sys.stderr)


if __name__ == '__main__':
    main()
