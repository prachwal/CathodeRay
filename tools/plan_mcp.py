#!/usr/bin/env python3
"""Serwer MCP dla narzędzia planu (tools/plan.py). Transport: stdio.

Rejestracja w opencode.json:
  "mcp": {"plan": {"type": "local", "command": ["python3", "tools/plan_mcp.py"]}}

Ścieżki względne planów liczone od roota repo (nie od cwd).
"""

import contextlib
import io
import json
import sys
from pathlib import Path
from types import SimpleNamespace

sys.path.insert(0, str(Path(__file__).resolve().parent))

from mcp.server.fastmcp import FastMCP  # noqa: E402

import plan as plan_cli  # noqa: E402

REPO = Path(__file__).resolve().parent.parent
mcp = FastMCP("plan")


def anchor(path: str) -> str:
    p = Path(path)
    return str(p if p.is_absolute() else REPO / p)


def call(fn, **kw) -> str:
    """Woła komendę plan.py; stdout/stderr jako wynik, exit != 0 jako tekst błędu."""
    if kw.get("plan"):
        kw["plan"] = anchor(kw["plan"])
    buf = io.StringIO()
    try:
        with contextlib.redirect_stdout(buf), contextlib.redirect_stderr(buf):
            fn(SimpleNamespace(**kw))
    except SystemExit as e:
        if e.code not in (0, None):
            return buf.getvalue().strip() or f"exit {e.code}"
    return buf.getvalue().strip()


@mcp.tool()
def plan_create(
    plan: str,
    title: str,
    items: list[str] | None = None,
    status: str = "open",
    items_file: str | None = None,
) -> str:
    """Utwórz plan: items to lista 'ID|Tytuł' (np. ['1|A', '1.1|B']); status open albo queued (gdy inny plan w toku).

    items_file: alternatywa dla items — plik JSON z array: stringi 'ID|Tytuł'
    albo obiekty {id, title[, note]} (np. draft z claude_table zapisany do pliku).
    """
    if status not in plan_cli.PLAN_STATUSES:
        return f"błąd: status planu musi być jednym z {plan_cli.PLAN_STATUSES}"
    merged = list(items or [])
    notes: dict[str, str] = {}
    if items_file:
        try:
            raw = json.loads(Path(anchor(items_file)).read_text(encoding="utf-8"))
        except (OSError, json.JSONDecodeError) as e:
            return f"błąd: items_file nieczytany: {e}"
        if not isinstance(raw, list):
            return "błąd: items_file musi zawierać JSON-array"
        for entry in raw:
            if isinstance(entry, str):
                merged.append(entry)
            elif isinstance(entry, dict) and isinstance(entry.get("id"), (str, int)):
                # float odpada: 1.10 z JSON-a to 1.1 — cicha kolizja ID
                item_id = str(entry["id"])
                merged.append(f"{item_id}|{entry.get('title') or ''}")
                if entry.get("note"):
                    # LLM wstawia \n w notach — rozbijają listę w renderze .md
                    notes[item_id] = " ".join(str(entry["note"]).split())
            else:
                return f"błąd: zły wpis w items_file: {entry!r} (string 'ID|Tytuł' albo {{id: str, title[, note]}})"
    out = call(
        plan_cli.cmd_create,
        plan=plan,
        title=title,
        item=merged,
        status=status,
        force=False,
    )
    if not out.startswith("utworzono"):
        return out  # create padł (np. plan istnieje) — noty nie mogą trafić w cudzy plan
    for item_id, note in notes.items():
        err = call(plan_cli.cmd_set, plan=plan, id=item_id, note=note)
        if "błąd:" in err:
            out += f"\nnota {item_id}: {err}"
    bare = [raw.partition("|")[0] for raw in merged if raw.partition("|")[0] not in notes]
    if bare:
        # miękko: plan powstaje, ale item bez bramki to detal do domknięcia przed pracą
        out += f"\nuwaga: {len(bare)} itemów bez note (Gotowe gdy:): {', '.join(bare)}"
    return out


@mcp.tool()
def plan_get(plan: str, id: str | None = None) -> str:
    """Pobierz element planu (id) lub wszystkie jako JSON."""
    return call(plan_cli.cmd_get, plan=plan, id=id)


@mcp.tool()
def plan_set(
    plan: str,
    id: str,
    status: str | None = None,
    title: str | None = None,
    note: str | None = None,
    append_note: str | None = None,
) -> str:
    """Zmień element: status (todo/partial/done), title, note (zamiana) albo append_note (dopisanie); co najmniej jedno pole."""
    return call(
        plan_cli.cmd_set,
        plan=plan,
        id=id,
        status=status,
        title=title,
        note=note,
        append_note=append_note,
    )


@mcp.tool()
def plan_check(plan: str, id: str) -> str:
    """Oznacz element jako gotowy (done)."""
    return plan_set(plan, id, status="done")


@mcp.tool()
def plan_uncheck(plan: str, id: str) -> str:
    """Oznacz element jako nie gotowy (todo)."""
    return plan_set(plan, id, status="todo")


@mcp.tool()
def plan_delete(plan: str, id: str) -> str:
    """Usuń element z planu."""
    return call(plan_cli.cmd_delete, plan=plan, id=id)


@mcp.tool()
def plan_add(plan: str, items: list[str], status: str = "todo", note: str = "") -> str:
    """Dodaj elementy do planu: items to lista 'ID|Tytuł'; status i note wspólne dla wszystkich."""
    if status not in plan_cli.STATUSES:
        return f"błąd: status musi być jednym z {plan_cli.STATUSES}"
    return call(plan_cli.cmd_add, plan=plan, item=items, status=status, note=note)


@mcp.tool()
def plan_status(plan: str, to: str | None = None) -> str:
    """Pobierz lub ustaw status planu: open (otwarty), active (w trakcie), queued (w kolejce), closed (zamknięty)."""
    if to is not None and to not in plan_cli.PLAN_STATUSES:
        return f"błąd: status planu musi być jednym z {plan_cli.PLAN_STATUSES}"
    return call(plan_cli.cmd_status, plan=plan, to=to)


@mcp.tool()
def plan_next(plan: str | None = None) -> str:
    """Pierwszy wykonalny element (nie-done, dzieci done) jako JSON; bez plan: plan w toku z docs/plans."""
    return call(plan_cli.cmd_next, plan=plan)


@mcp.tool()
def plan_hygiene() -> str:
    """Sprawdź księgę docs/plans: statusy, jeden plan w toku, .md zgodne z JSON, rodzic done tylko z dziećmi done."""
    return call(plan_cli.cmd_hygiene, dir=None)


@mcp.tool()
def plan_render(plan: str) -> str:
    """Zwróć checklistę .md planu jako tekst (i zapisz obok JSON)."""
    out = str(Path(anchor(plan)).with_suffix(".md"))
    res = call(plan_cli.cmd_render, plan=plan, out=out)
    body = Path(out).read_text(encoding="utf-8") if Path(out).exists() else ""
    return f"{res}\n\n{body}".strip()


if __name__ == "__main__":
    mcp.run()
