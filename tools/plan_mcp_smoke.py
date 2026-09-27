#!/usr/bin/env python3
"""Smoke serwera MCP plan: uruchamia go przez stdio i sprawdza kontrakt.

Kontrakt: serwer rejestruje 9 narzędzi zgodnych z CLI i `plan_status` odpowiada.
Wykrywa regresję po stronie serwera (brakujące/zdublowane toole). Ekspozycja po
stronie opencode zależy od restartu klienta po zmianie serwera — to nie ten test.

Użycie: python3 tools/plan_mcp_smoke.py
"""

import asyncio
import sys
import tempfile
from pathlib import Path

from mcp import ClientSession, StdioServerParameters
from mcp.client.stdio import stdio_client

REPO = Path(__file__).resolve().parent.parent
EXPECTED = {
    "plan_create",
    "plan_get",
    "plan_set",
    "plan_check",
    "plan_uncheck",
    "plan_delete",
    "plan_add",
    "plan_status",
    "plan_render",
    "plan_next",
    "plan_hygiene",
}


async def main() -> int:
    params = StdioServerParameters(
        command=sys.executable, args=[str(REPO / "tools" / "plan_mcp.py")]
    )
    async with stdio_client(params) as (read, write):
        async with ClientSession(read, write) as session:
            await session.initialize()
            tools = await session.list_tools()
            names = {tool.name for tool in tools.tools}

            missing = EXPECTED - names
            extra = names - EXPECTED
            if missing or extra:
                print(
                    f"FAIL: brak={sorted(missing)} nadmiar={sorted(extra)}",
                    file=sys.stderr,
                )
                return 1

            # Samowystarczalny: plan w katalogu tymczasowym, bez zależności od repo.
            with tempfile.TemporaryDirectory() as tmp:
                plan = str(Path(tmp) / "smoke.json")
                created = await session.call_tool(
                    "plan_create", {"plan": plan, "title": "smoke", "items": []}
                )
                result = await session.call_tool("plan_status", {"plan": plan})
                if created.isError or result.isError:
                    print("FAIL: plan_create/plan_status zwrócił błąd", file=sys.stderr)
                    return 1

                text = getattr(result.content[0], "text", "") if result.content else ""
                if text != "open":
                    print(f"FAIL: plan_status -> {text!r}", file=sys.stderr)
                    return 1

                draft = Path(tmp) / "draft.json"
                draft.write_text(
                    '["9|X", {"id": "9.1", "title": "Y", "note": "bramka\\n\\nx"}]',
                    encoding="utf-8",
                )
                from_file = await session.call_tool(
                    "plan_create",
                    {
                        "plan": str(Path(tmp) / "smoke-file.json"),
                        "title": "smoke-file",
                        "items_file": str(draft),
                    },
                )
                got = await session.call_tool(
                    "plan_get", {"plan": str(Path(tmp) / "smoke-file.json")}
                )
                got_text = getattr(got.content[0], "text", "") if got.content else ""
                from_text = getattr(from_file.content[0], "text", "")
                if "uwaga: 1 itemów bez note" not in from_text or ": 9" not in from_text:
                    print(f"FAIL: ostrzeżenie bez note -> {from_text!r}", file=sys.stderr)
                    return 1
                if from_file.isError or '"id": "9.1"' not in got_text:
                    print(f"FAIL: items_file -> {got_text!r}", file=sys.stderr)
                    return 1
                if '"note": "bramka x"' not in got_text:
                    print(f"FAIL: items_file note -> {got_text!r}", file=sys.stderr)
                    return 1

                other = Path(tmp) / "other.json"
                other.write_text(
                    '[{"id": "9.1", "title": "Y", "note": "obca"}]', encoding="utf-8"
                )
                again = await session.call_tool(
                    "plan_create",
                    {
                        "plan": str(Path(tmp) / "smoke-file.json"),
                        "title": "smoke-file",
                        "items_file": str(other),
                    },
                )
                kept = await session.call_tool(
                    "plan_get", {"plan": str(Path(tmp) / "smoke-file.json")}
                )
                again_text = getattr(again.content[0], "text", "")
                if "istnieje" not in again_text or kept.content[0].text != got_text:
                    print(f"FAIL: items_file na istniejący -> {again_text!r}", file=sys.stderr)
                    return 1

                bad = await session.call_tool(
                    "plan_create",
                    {
                        "plan": str(Path(tmp) / "smoke-bad.json"),
                        "title": "smoke-bad",
                        "items_file": str(Path(tmp) / "nie-ma.json"),
                    },
                )
                if bad.isError or "nieczytany" not in getattr(
                    bad.content[0], "text", ""
                ):
                    print("FAIL: items_file brak pliku", file=sys.stderr)
                    return 1

                edited = await session.call_tool(
                    "plan_add", {"plan": plan, "items": ["1|A", "1.1|B"], "note": "n"}
                )
                renamed = await session.call_tool(
                    "plan_set",
                    {"plan": plan, "id": "1.1", "title": "B2", "append_note": "m"},
                )
                nxt = await session.call_tool("plan_next", {"plan": plan})
                nxt_text = getattr(nxt.content[0], "text", "") if nxt.content else ""
                if edited.isError or renamed.isError or '"id": "1.1"' not in nxt_text:
                    print(
                        f"FAIL: plan_add/plan_set/plan_next -> {nxt_text!r}",
                        file=sys.stderr,
                    )
                    return 1
                if '"note": "n · m"' not in nxt_text:
                    print(f"FAIL: append_note -> {nxt_text!r}", file=sys.stderr)
                    return 1

            hygiene = await session.call_tool("plan_hygiene", {})
            hygiene_text = (
                getattr(hygiene.content[0], "text", "") if hygiene.content else ""
            )
            if "hygiene OK" not in hygiene_text:
                print(f"FAIL: plan_hygiene -> {hygiene_text!r}", file=sys.stderr)
                return 1

            print(
                f"OK: {len(names)} narzędzi, plan_status -> {text}, next/set/add/hygiene"
            )
            return 0


if __name__ == "__main__":
    raise SystemExit(asyncio.run(main()))
