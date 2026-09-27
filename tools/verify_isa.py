#!/usr/bin/env python3
"""Bramka: walidacja plików ISA (data/instructions/*.json) względem isa.schema.json.

Sprawdza:
  - poprawność samego schematu (draft 2020-12),
  - zgodność każdego pliku ze schematem (pola, typy, wzorce opcodów),
  - unikalność opcode w obrębie pliku (JSON Schema tego nie wyraża).

Użycie: python3 tools/verify_isa.py   (exit 0 = OK, 1 = naruszenia)
"""

from __future__ import annotations

import json
import sys
from pathlib import Path

try:
    from jsonschema import Draft202012Validator
except ImportError:
    print("Error: brak jsonschema. Instalacja: pip install -r tools/requirements.txt", file=sys.stderr)
    raise SystemExit(2)

REPO = Path(__file__).resolve().parent.parent
DIR = REPO / "data" / "instructions"
SCHEMA = DIR / "isa.schema.json"


def main() -> int:
    schema = json.loads(SCHEMA.read_text(encoding="utf-8"))
    Draft202012Validator.check_schema(schema)
    validator = Draft202012Validator(schema)

    files = sorted(p for p in DIR.glob("*.json") if p.name != SCHEMA.name)
    problems = 0
    for path in files:
        data = json.loads(path.read_text(encoding="utf-8"))
        errors = sorted(validator.iter_errors(data), key=lambda e: list(e.path))
        for err in errors:
            print(f"{path.name}: [{'.'.join(map(str, err.path))}] {err.message}", file=sys.stderr)

        opcodes = [i.get("opcode") for i in data.get("instructions", [])]
        duplicates = sorted({o for o in opcodes if opcodes.count(o) > 1})
        for op in duplicates:
            print(f"{path.name}: duplikat opcode {op}", file=sys.stderr)

        problems += len(errors) + len(duplicates)
        if not errors and not duplicates:
            print(f"{path.name}: OK ({len(opcodes)} opcodów)")

    if problems:
        print(f"verify_isa: {problems} problemów", file=sys.stderr)
        return 1
    print(f"verify_isa: OK ({len(files)} plików)")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
