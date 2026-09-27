#!/usr/bin/env python3
"""Serwer MCP dla zestawów instrukcji 4004/4040/CHIP-8. Transport: stdio.

Rejestracja w opencode.json:
  "mcp": {"instructions": {"type": "local", "command": ["python3", "tools/mcp_instructions.py"]}}

JSON jest źródłem prawdy (data/instructions/mcp_*_instructions.json);
MD renderuje tools/generate_opcode_tables.py. Ścieżki liczone od roota repo.
"""

from __future__ import annotations

import json
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
}


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


if __name__ == "__main__":
    mcp.run()
