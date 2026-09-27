#!/usr/bin/env python3
"""Smoke serwera MCP `instructions`: kontrakt (lista tooli) + zachowanie każdego z nich.

Uruchamia serwer po stdio i sprawdza: odczyt/statystyki, walidację schematem,
pokrycie, listę niezweryfikowanych, diff, render oraz importer (bez efektów ubocznych).

Użycie: python3 tools/instructions_mcp_smoke.py
"""

import asyncio
import json
import sys
from pathlib import Path

from mcp import ClientSession, StdioServerParameters
from mcp.client.stdio import stdio_client

REPO = Path(__file__).resolve().parent.parent
EXPECTED = {
    "processors",
    "add",
    "delete",
    "modify",
    "read",
    "list",
    "by-operand",
    "search",
    "stats",
    "info",
    "schema",
    "validate",
    "coverage",
    "unverified",
    "diff",
    "render",
    "import",
}


def as_json(result) -> dict:
    text = getattr(result.content[0], "text", "") if result.content else ""
    return json.loads(text)


async def main() -> int:
    params = StdioServerParameters(
        command=sys.executable, args=[str(REPO / "tools" / "mcp_instructions.py")]
    )
    async with stdio_client(params) as (read, write):
        async with ClientSession(read, write) as session:
            await session.initialize()
            names = {t.name for t in (await session.list_tools()).tools}
            missing, extra = EXPECTED - names, names - EXPECTED
            if missing or extra:
                print(
                    f"FAIL: brak={sorted(missing)} nadmiar={sorted(extra)}",
                    file=sys.stderr,
                )
                return 1

            async def call(tool: str, args: dict):
                return await session.call_tool(tool, args)

            procs = as_json(await call("processors", {}))
            if (
                not procs["success"]
                or "6502" not in procs["processors"]
                or "1802" not in procs["processors"]
            ):
                print(f"FAIL: processors -> {procs}", file=sys.stderr)
                return 1

            read = as_json(await call("read", {"processor": "6502", "opcode": "A9"}))
            if read.get("instruction", {}).get("mnemonic") != "LDA":
                print(f"FAIL: read 6502 A9 -> {read}", file=sys.stderr)
                return 1

            stats = as_json(await call("stats", {"processor": "chip8"}))
            if stats.get("total") != 35:
                print(f"FAIL: stats chip8 -> {stats}", file=sys.stderr)
                return 1

            validate = as_json(await call("validate", {}))
            if not validate["success"] or validate["checked"] != 9:
                print(f"FAIL: validate -> {validate}", file=sys.stderr)
                return 1
            bad = {k: v for k, v in validate["results"].items() if not v["ok"]}
            if bad:
                print(f"FAIL: validate zgłasza błędy -> {bad}", file=sys.stderr)
                return 1

            cov = as_json(await call("coverage", {"processor": "8080"}))
            if not cov["complete"] or cov["missing"]:
                print(f"FAIL: coverage 8080 -> {cov}", file=sys.stderr)
                return 1
            cov8 = as_json(await call("coverage", {"processor": "chip8"}))
            if cov8["pattern_opcodes"] != 35 or cov8["complete"]:
                print(f"FAIL: coverage chip8 -> {cov8}", file=sys.stderr)
                return 1

            un = as_json(await call("unverified", {"processor": "6502"}))
            if un["processors"]["6502"]["unverified"] != 256:
                print(f"FAIL: unverified 6502 -> {un}", file=sys.stderr)
                return 1

            diff = as_json(await call("diff", {"a": "6502", "b": "65c02"}))
            if sum(diff["counts"].values()) == 0:
                print(f"FAIL: diff 6502/65c02 pusty -> {diff}", file=sys.stderr)
                return 1

            render = await call("render", {"processor": "6800"})
            render_text = (
                getattr(render.content[0], "text", "") if render.content else ""
            )
            if "| Opcode |" not in render_text or "BRN" not in render_text:
                print("FAIL: render 6800", file=sys.stderr)
                return 1

            bad_import = as_json(await call("import", {"cpu": "z80"}))
            if "error" not in bad_import:
                print(
                    f"FAIL: import z80 powinien zwrócić błąd -> {bad_import}",
                    file=sys.stderr,
                )
                return 1

            print(
                f"OK: {len(names)} narzędzi; validate={validate['checked']}, "
                f"coverage 8080={cov['complete']}, unverified 6502={un['processors']['6502']['unverified']}, "
                f"diff 6502/65c02={diff['counts']}"
            )
            return 0


if __name__ == "__main__":
    raise SystemExit(asyncio.run(main()))
