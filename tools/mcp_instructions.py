#!/usr/bin/env python3
"""Serwer MCP dla zestawów instrukcji 4004/4040/CHIP-8. Transport: stdio.

Rejestracja w opencode.json:
  "mcp": {"instructions": {"type": "local", "command": ["python3", "tools/mcp_instructions.py"]}}

JSON jest źródłem prawdy (data/instructions/mcp_*_instructions.json);
MD renderuje tools/generate_opcode_tables.py. Ścieżki liczone od roota repo.
"""

from __future__ import annotations

import json
import subprocess
import sys
from pathlib import Path
from typing import Any

try:
    from mcp.server.fastmcp import FastMCP
except ImportError:
    print(
        "Error: mcp.server.fastmcp not found. Install with: pip install -r tools/requirements.txt",
        file=sys.stderr,
    )
    sys.exit(1)

REPO = Path(__file__).resolve().parent.parent
mcp = FastMCP("instructions")

STORES = {
    "4004": REPO / "data/instructions/mcp_4004_instructions.json",
    "4040": REPO / "data/instructions/mcp_4040_instructions.json",
    "chip8": REPO / "data/instructions/mcp_chip8_instructions.json",
    "6502": REPO / "data/instructions/mcp_6502_instructions.json",
    "65c02": REPO / "data/instructions/mcp_65c02_instructions.json",
    "8080": REPO / "data/instructions/mcp_8080_instructions.json",
    "6800": REPO / "data/instructions/mcp_6800_instructions.json",
    "1802": REPO / "data/instructions/cosmac_vip_cdp1802_isa.json",
}

ISA_SCHEMA = REPO / "data/instructions" / "isa.schema.json"
IMPORT_CPUS = ("6502", "65c02", "8080", "6800", "1802")


def _check(processor: str) -> Path:
    if processor not in STORES:
        raise ValueError(f"Unknown processor: {processor}. Valid: {', '.join(STORES)}")
    p = STORES[processor]
    if not p.exists():
        raise FileNotFoundError(f"Instructions file not found: {p}")
    return p


def _load(processor: str) -> dict[str, Any]:
    return json.loads(_check(processor).read_text(encoding="utf-8"))


def _save(processor: str, data: dict[str, Any]) -> None:
    _check(processor).write_text(
        json.dumps(data, indent=2, ensure_ascii=False) + "\n", encoding="utf-8"
    )


def _dump(result: dict[str, Any]) -> str:
    return json.dumps(result, ensure_ascii=False)


@mcp.tool(name="processors")
def processors_tool() -> str:
    """Lista dostępnych procesorów i ścieżek do JSON."""
    return _dump(
        {
            "success": True,
            "processors": list(STORES),
            "stores": {k: str(v) for k, v in STORES.items()},
        }
    )


@mcp.tool(name="add")
def add_tool(
    processor: str,
    opcode: str,
    mnemonic: str,
    cycles: int,
    words: int,
    semantics: str,
    group: str,
    encoding: str | None = None,
    operands: list[dict[str, Any]] | None = None,
    variants: list[str] | None = None,
) -> str:
    """Dodaj instrukcję (błąd gdy opcode już istnieje)."""
    try:
        data = _load(processor)
    except (ValueError, FileNotFoundError) as e:
        return _dump({"error": str(e)})
    if any(i["opcode"] == opcode for i in data["instructions"]):
        return _dump({"error": f"Instruction {opcode} already exists"})
    data["instructions"].append(
        {
            "opcode": opcode,
            "mnemonic": mnemonic,
            "cycles": cycles,
            "words": words,
            "semantics": semantics,
            "group": group,
            "encoding": encoding,
            "operands": operands,
            "variants": variants,
        }
    )
    _save(processor, data)
    return _dump(
        {"success": True, "message": f"Added {processor}/{opcode} ({mnemonic})"}
    )


@mcp.tool(name="delete")
def delete_tool(processor: str, opcode: str) -> str:
    """Usuń instrukcję po opcode."""
    try:
        data = _load(processor)
    except (ValueError, FileNotFoundError) as e:
        return _dump({"error": str(e)})
    before = len(data["instructions"])
    data["instructions"] = [i for i in data["instructions"] if i["opcode"] != opcode]
    if len(data["instructions"]) == before:
        return _dump({"error": f"Instruction {opcode} not found"})
    _save(processor, data)
    return _dump({"success": True, "message": f"Deleted {processor}/{opcode}"})


@mcp.tool(name="modify")
def modify_tool(
    processor: str,
    opcode: str,
    mnemonic: str | None = None,
    cycles: int | None = None,
    words: int | None = None,
    semantics: str | None = None,
    group: str | None = None,
    encoding: str | None = None,
    operands: list[dict[str, Any]] | None = None,
    variants: list[str] | None = None,
) -> str:
    """Zmień pola instrukcji (tylko podane, nie-None)."""
    try:
        data = _load(processor)
    except (ValueError, FileNotFoundError) as e:
        return _dump({"error": str(e)})
    for instr in data["instructions"]:
        if instr["opcode"] == opcode:
            patch = {
                "mnemonic": mnemonic,
                "cycles": cycles,
                "words": words,
                "semantics": semantics,
                "group": group,
                "encoding": encoding,
                "operands": operands,
                "variants": variants,
            }
            instr.update({k: v for k, v in patch.items() if v is not None})
            _save(processor, data)
            return _dump({"success": True, "message": f"Modified {processor}/{opcode}"})
    return _dump({"error": f"Instruction {opcode} not found"})


@mcp.tool(name="read")
def read_tool(processor: str, opcode: str) -> str:
    """Odczytaj jedną instrukcję po opcode."""
    try:
        data = _load(processor)
    except (ValueError, FileNotFoundError) as e:
        return _dump({"error": str(e)})
    for instr in data["instructions"]:
        if instr["opcode"] == opcode:
            return _dump({"success": True, "instruction": instr})
    return _dump({"error": f"Instruction {opcode} not found"})


@mcp.tool(name="list")
def list_tool(
    processor: str, group: str | None = None, operand: str | None = None
) -> str:
    """Lista instrukcji, opcjonalnie filtr grupa i/lub typ operandu."""
    try:
        data = _load(processor)
    except (ValueError, FileNotFoundError) as e:
        return _dump({"error": str(e)})
    instructions = data["instructions"]
    if group:
        instructions = [i for i in instructions if i.get("group") == group]
    if operand:
        want = operand.lower()
        instructions = [
            i
            for i in instructions
            if any(want == o.get("type", "").lower() for o in (i.get("operands") or []))
        ]
    return _dump(
        {
            "success": True,
            "processor": processor,
            "count": len(instructions),
            "instructions": instructions,
        }
    )


@mcp.tool(name="by-operand")
def by_operand_tool(processor: str, operand: str) -> str:
    """Instrukcje przyjmujące operand danego typu (register, pair, immediate, address, condition)."""
    return list_tool(processor, operand=operand)


@mcp.tool(name="search")
def search_tool(processor: str, query: str) -> str:
    """Szukaj po mnemonic, semantics, encoding, operandach."""
    try:
        data = _load(processor)
    except (ValueError, FileNotFoundError) as e:
        return _dump({"error": str(e)})
    q = query.lower()

    def matches(i: dict[str, Any]) -> bool:
        if q in i.get("mnemonic", "").lower() or q in i.get("semantics", "").lower():
            return True
        if q in (i.get("encoding") or "").lower():
            return True
        return any(
            q in o.get("name", "").lower() or q in o.get("type", "").lower()
            for o in (i.get("operands") or [])
        )

    results = [i for i in data["instructions"] if matches(i)]
    return _dump(
        {
            "success": True,
            "processor": processor,
            "count": len(results),
            "instructions": results,
        }
    )


@mcp.tool(name="stats")
def stats_tool(processor: str) -> str:
    """Statystyki zestawu: liczba, podział na grupy, suma cykli/słów."""
    try:
        data = _load(processor)
    except (ValueError, FileNotFoundError) as e:
        return _dump({"error": str(e)})
    instructions = data["instructions"]
    by_group: dict[str, int] = {}
    for instr in instructions:
        by_group[instr["group"]] = by_group.get(instr["group"], 0) + 1
    return _dump(
        {
            "success": True,
            "processor": processor,
            "total": len(instructions),
            "by_group": by_group,
            "total_cycles": sum(i["cycles"] for i in instructions),
            "total_words": sum(i["words"] for i in instructions),
        }
    )


@mcp.tool(name="info")
def info_tool(processor: str) -> str:
    """Metadane procesora (description, notes, format_version, schema)."""
    try:
        data = _load(processor)
    except (ValueError, FileNotFoundError) as e:
        return _dump({"error": str(e)})
    return _dump(
        {
            "success": True,
            "processor": processor,
            "description": data.get("description"),
            "notes": data.get("notes"),
            "format_version": data.get("format_version"),
            "schema": data.get("schema"),
            "instruction_count": len(data.get("instructions", [])),
        }
    )


@mcp.tool(name="schema")
def schema_tool(processor: str) -> str:
    """Schemat rekordu instrukcji dla procesora."""
    try:
        data = _load(processor)
    except (ValueError, FileNotFoundError) as e:
        return _dump({"error": str(e)})
    if not data.get("schema"):
        return _dump({"error": f"No schema defined for {processor}"})
    return _dump({"success": True, "processor": processor, "schema": data["schema"]})


@mcp.tool(name="validate")
def validate_tool(processor: str | None = None) -> str:
    """Waliduj pliki ISA: zgodność z isa.schema.json + unikalność opcode. Bez argumentu = wszystkie."""
    try:
        from jsonschema import Draft202012Validator
    except ImportError:
        return _dump({"error": "brak jsonschema (pip install -r tools/requirements.txt)"})
    validator = Draft202012Validator(json.loads(ISA_SCHEMA.read_text(encoding="utf-8")))
    targets = [processor] if processor else list(STORES)
    results: dict[str, Any] = {}
    ok = True
    for name in targets:
        if name not in STORES:
            results[name] = {"ok": False, "errors": [f"unknown processor {name}"]}
            ok = False
            continue
        try:
            data = _load(name)
        except (ValueError, FileNotFoundError) as e:
            results[name] = {"ok": False, "errors": [str(e)]}
            ok = False
            continue
        errors = [
            f"[{'.'.join(map(str, e.path))}] {e.message}"
            for e in validator.iter_errors(data)
        ]
        ops = [i.get("opcode") for i in data.get("instructions", [])]
        dup = sorted({o for o in ops if ops.count(o) > 1})
        if dup:
            errors.append(f"duplikaty opcode: {', '.join(dup)}")
        results[name] = {"ok": not errors, "errors": errors}
        ok = ok and not errors
    return _dump({"success": ok, "checked": len(targets), "results": results})


@mcp.tool(name="coverage")
def coverage_tool(processor: str) -> str:
    """Pokrycie opcodów: 2-hex obecne vs brakujące 00-FF; wzorce (np. CHIP-8) raportowane osobno."""
    try:
        data = _load(processor)
    except (ValueError, FileNotFoundError) as e:
        return _dump({"error": str(e)})
    ops = [i["opcode"] for i in data["instructions"]]
    hexops = {o for o in ops if len(o) == 2}
    patterns = sorted(o for o in ops if len(o) != 2)
    present = {f"{int(o, 16):02X}" for o in hexops}
    missing = sorted(f"{x:02X}" for x in range(256) if f"{x:02X}" not in present)
    return _dump(
        {
            "success": True,
            "processor": processor,
            "total": len(ops),
            "hex_opcodes": len(hexops),
            "pattern_opcodes": len(patterns),
            "patterns": patterns,
            "missing": missing,
            "complete": not missing,
        }
    )


@mcp.tool(name="unverified")
def unverified_tool(processor: str | None = None, limit: int = 100) -> str:
    """Opcody bez prefiksu 'Verified:' w semantics (postęp implementacji). Bez argumentu = wszystkie."""
    targets = [processor] if processor else list(STORES)
    per: dict[str, Any] = {}
    total = 0
    for name in targets:
        try:
            data = _load(name)
        except (ValueError, FileNotFoundError) as e:
            per[name] = {"error": str(e)}
            continue
        items = [
            {"opcode": i["opcode"], "mnemonic": i["mnemonic"]}
            for i in data["instructions"]
            if not str(i.get("semantics") or "").startswith("Verified:")
        ]
        per[name] = {
            "unverified": len(items),
            "total": len(data["instructions"]),
            "items": items[:limit],
            "truncated": len(items) > limit,
        }
        total += len(items)
    return _dump({"success": True, "total_unverified": total, "processors": per})


@mcp.tool(name="diff")
def diff_tool(a: str, b: str) -> str:
    """Porównaj dwa zestawy po opcode: dodane/usunięte/zmienione (mnemonic/cycles/words/group/semantics)."""
    try:
        ia = {i["opcode"]: i for i in _load(a)["instructions"]}
        ib = {i["opcode"]: i for i in _load(b)["instructions"]}
    except (ValueError, FileNotFoundError, KeyError) as e:
        return _dump({"error": str(e)})
    changed = []
    for op in sorted(set(ia) & set(ib)):
        fields = {
            f: [ia[op].get(f), ib[op].get(f)]
            for f in ("mnemonic", "cycles", "words", "group", "semantics")
            if ia[op].get(f) != ib[op].get(f)
        }
        if fields:
            changed.append({"opcode": op, "from": ia[op]["mnemonic"], "to": ib[op]["mnemonic"], "fields": fields})
    return _dump(
        {
            "success": True,
            "a": a,
            "b": b,
            "added": sorted(set(ib) - set(ia)),
            "removed": sorted(set(ia) - set(ib)),
            "changed": changed,
            "counts": {"added": len(set(ib) - set(ia)), "removed": len(set(ia) - set(ib)), "changed": len(changed)},
        }
    )


@mcp.tool(name="render")
def render_tool(processor: str) -> str:
    """Wyrenderuj tabelę Markdown instrukcji (opcode/mnemonic/cykle/słowa/grupa/semantyka)."""
    try:
        data = _load(processor)
    except (ValueError, FileNotFoundError) as e:
        return _dump({"error": str(e)})
    lines = [
        f"# {data.get('processor', processor)} — tabela opcodów",
        "",
        "| Opcode | Mnemonic | Cykle | Słowa | Grupa | Semantyka |",
        "| --- | --- | --- | --- | --- | --- |",
    ]
    for i in data["instructions"]:
        sem = str(i.get("semantics") or "").replace("|", "\\|")
        lines.append(
            f"| `{i['opcode']}` | `{i['mnemonic']}` | {i['cycles']} | {i['words']} | {i['group']} | {sem} |"
        )
    return "\n".join(lines)


@mcp.tool(name="import")
def import_tool(cpu: str) -> str:
    """Uruchom importer (tools/import_isa_json.py) dla CPU i zwróć wynik."""
    if cpu not in IMPORT_CPUS:
        return _dump({"error": f"brak adaptera dla {cpu}; dostępne: {', '.join(IMPORT_CPUS)}"})
    proc = subprocess.run(
        [sys.executable, str(REPO / "tools" / "import_isa_json.py"), cpu],
        capture_output=True, text=True, cwd=str(REPO),
    )
    return _dump(
        {
            "success": proc.returncode == 0,
            "returncode": proc.returncode,
            "stdout": proc.stdout.strip(),
            "stderr": proc.stderr.strip(),
        }
    )


if __name__ == "__main__":
    mcp.run()
