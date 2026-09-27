#!/usr/bin/env python3
"""Plan zadań (docs/plans): JSON źródłem prawdy, .md renderem do wglądu.

Użycie:
  plan.py create PLAN.json --title "Tytuł" --item "1|Krok" [--status queued]
  plan.py get PLAN.json [ID]            # element lub wszystkie, jako JSON
  plan.py set PLAN.json ID [--status todo|partial|done] [--title T] [--note N | --append-note N]
  plan.py next [PLAN.json]              # pierwszy wykonalny element (domyślnie plan w toku)
  plan.py check PLAN.json ID            # to samo co set --status done
  plan.py uncheck PLAN.json ID          # to samo co set --status todo
  plan.py delete PLAN.json ID           # usuń element (alias: rm)
  plan.py status PLAN.json [--to open|active|queued|closed]  # status planu
  plan.py render PLAN.json [--out PLAN.md]
  plan.py hygiene [KATALOG]             # księga planów vs reguły (domyślnie docs/plans)
  plan.py selftest

Statusy podpunktu: todo / partial / done.
Status planu: open (jedyny ruszany), active (w trakcie), queued (kolejka),
closed (wszystkie podpunkty done).
"""

import argparse
import json
import re
import sys
import tempfile
from pathlib import Path
from typing import NoReturn

STATUSES = ("todo", "partial", "done")
PLAN_STATUSES = ("open", "active", "queued", "closed")
IN_FLIGHT = frozenset({"open", "active"})
PLAN_LABELS = {
    "open": "otwarty",
    "active": "w trakcie",
    "queued": "w kolejce",
    "closed": "zamknięty",
}
ID_RE = re.compile(r"^\d+(\.\d+)*$")
PLAN_FILE_RE = re.compile(r"^(\d+)-[a-z0-9-]+\.json$")
# Typ generyczny poza backtickami (Foo<T>, Func<A, B<C>>) — markdownlint MD033 bierze go za HTML.
_GENERIC_ARG = r"[\w.?\[\]]+(?:<[\w.?\[\], ]+>)?"
GENERIC_RE = re.compile(rf"\b[A-Za-z_][\w.]*<{_GENERIC_ARG}(?:,\s?{_GENERIC_ARG})*>")
NOTE_SEP = " · "


def fail(msg: str) -> NoReturn:
    print(f"plan.py: błąd: {msg}", file=sys.stderr)
    sys.exit(1)


def load(path: str) -> dict:
    try:
        plan = json.loads(Path(path).read_text(encoding="utf-8"))
    except FileNotFoundError:
        fail(f"nie ma pliku: {path}")
    except json.JSONDecodeError as e:
        fail(f"nieprawidłowy JSON ({path}): {e}")
    if not isinstance(plan.get("items"), list):
        fail(f"{path}: brak listy 'items'")
    return plan


def save(path: str, plan: dict) -> None:
    """Zapis JSON + render .md obok: każda mutacja zostawia checklistę w zgodzie."""
    Path(path).write_text(
        json.dumps(plan, ensure_ascii=False, indent=2) + "\n", encoding="utf-8"
    )
    Path(path).with_suffix(".md").write_text(render_text(plan), encoding="utf-8")


def find(plan: dict, item_id: str) -> dict:
    for item in plan["items"]:
        if item["id"] == item_id:
            return item
    fail(f"brak elementu '{item_id}'")


def sort_key(item_id: str) -> tuple:
    return tuple(int(p) for p in item_id.split("."))


def repo_root() -> Path:
    return Path(__file__).resolve().parent.parent


def default_plans_dir() -> Path:
    return repo_root() / "docs" / "plans"


def is_catalog(path: Path) -> bool:
    """True, gdy plik żyje w katalogu księgi (docs/plans), nie w tmp/selftest."""
    try:
        return path.resolve().parent == default_plans_dir().resolve()
    except OSError:
        return False


def list_plan_json(plans_dir: Path) -> list[Path]:
    return sorted(p for p in plans_dir.glob("*.json") if p.is_file())


def in_flight_files(plans_dir: Path, exclude: Path | None = None) -> list[Path]:
    found = []
    skip = exclude.resolve() if exclude is not None else None
    for path in list_plan_json(plans_dir):
        if skip is not None and path.resolve() == skip:
            continue
        plan = json.loads(path.read_text(encoding="utf-8"))
        if plan.get("status") in IN_FLIGHT:
            found.append(path)
    return found


def refuse_second_in_flight(path: Path, new_status: str) -> None:
    if new_status not in IN_FLIGHT or not is_catalog(path):
        return
    others = in_flight_files(path.parent, exclude=path)
    if others:
        names = ", ".join(p.name for p in others)
        fail(
            f"już jest plan w locie ({names}); ten zostaw queued albo zamknij tamten"
        )


def refuse_duplicate_number(path: Path) -> None:
    if not is_catalog(path):
        return
    match = PLAN_FILE_RE.match(path.name)
    if not match:
        fail(f"nazwa planu: NN-slug.json (cyfry, hyphen), nie {path.name!r}")
    number = match.group(1)
    for other in list_plan_json(path.parent):
        if other.resolve() == path.resolve():
            continue
        other_match = PLAN_FILE_RE.match(other.name)
        if other_match and other_match.group(1) == number:
            fail(f"numer {number} zajęty przez {other.name}")


def children(plan: dict, item_id: str) -> list[dict]:
    prefix = item_id + "."
    return [i for i in plan["items"] if i["id"].startswith(prefix)]


def next_item(plan: dict) -> dict | None:
    """Pierwszy nie-done element (po id), którego wszystkie dzieci są done."""
    for item in sorted(plan["items"], key=lambda i: sort_key(i["id"])):
        if item["status"] == "done":
            continue
        if any(c["status"] != "done" for c in children(plan, item["id"])):
            continue
        return item
    return None


def all_items_done(plan: dict) -> bool:
    items = plan.get("items") or []
    return bool(items) and all(i.get("status") == "done" for i in items)


def hygiene_errors(plans_dir: Path) -> list[str]:
    """Niespójności księgi. Pusty wynik = bramka zielona."""
    errors: list[str] = []
    if not plans_dir.is_dir():
        return [f"brak katalogu {plans_dir}"]
    in_flight: list[str] = []
    for path in list_plan_json(plans_dir):
        try:
            plan = json.loads(path.read_text(encoding="utf-8"))
        except json.JSONDecodeError as e:
            errors.append(f"{path.name}: JSON {e}")
            continue
        status = plan.get("status")
        if status not in PLAN_STATUSES:
            errors.append(f"{path.name}: brak statusu planu (open/active/queued/closed)")
            continue
        if status in IN_FLIGHT:
            in_flight.append(path.name)
        if status == "closed" and not all_items_done(plan):
            undone = [
                i["id"]
                for i in plan.get("items") or []
                if i.get("status") != "done"
            ]
            errors.append(f"{path.name}: closed, a nie-done: {', '.join(undone)}")
        for item in plan.get("items") or []:
            if item.get("status") != "done":
                continue
            open_kids = [c["id"] for c in children(plan, item["id"]) if c["status"] != "done"]
            if open_kids:
                errors.append(
                    f"{path.name}: {item['id']} done, a dzieci nie: {', '.join(open_kids)}"
                )
        md = path.with_suffix(".md")
        if not md.is_file():
            errors.append(f"{path.name}: brak {md.name} (plan.py render)")
        elif md.read_text(encoding="utf-8") != render_text(plan):
            errors.append(f"{md.name}: nieaktualny względem JSON (plan.py render)")
    json_stem = {p.stem for p in list_plan_json(plans_dir)}
    for md in sorted(plans_dir.glob("*.md")):
        if md.stem in json_stem:
            continue
        errors.append(f"{md.name}: .md bez JSON (plan tylko przez plan.py)")
    if len(in_flight) > 1:
        errors.append("więcej niż jeden plan open/active: " + ", ".join(in_flight))
    return errors


def cmd_create(args):
    if Path(args.plan).exists() and not args.force:
        fail(f"{args.plan} już istnieje (użyj --force)")
    status = getattr(args, "status", None) or "open"
    if status not in PLAN_STATUSES:
        fail(f"status planu musi być jednym z {PLAN_STATUSES}")
    dest = Path(args.plan)
    refuse_duplicate_number(dest)
    refuse_second_in_flight(dest, status)
    items, seen = [], set()
    for raw in args.item:
        item_id, sep, title = raw.partition("|")
        if not sep or not title.strip() or not ID_RE.match(item_id) or item_id in seen:
            fail(f"zły --item (oczekiwano 'ID|Tytuł', ID unikalne jak 1, 1.2): {raw!r}")
        seen.add(item_id)
        items.append(
            {"id": item_id, "title": title.strip(), "status": "todo", "note": ""}
        )
    items.sort(key=lambda i: sort_key(i["id"]))
    save(args.plan, {"title": args.title, "status": status, "items": items})
    print(f"utworzono {args.plan} ({len(items)} elementów, {status})")


def cmd_get(args):
    plan = load(args.plan)
    plan.setdefault("status", "open")  # plany sprzed statusów
    result = find(plan, args.id) if args.id else plan
    print(json.dumps(result, ensure_ascii=False, indent=2))


def cmd_set(args):
    status = getattr(args, "status", None)
    title = getattr(args, "title", None)
    note = getattr(args, "note", None)
    append = getattr(args, "append_note", None)
    if status is not None and status not in STATUSES:
        fail(f"status musi być jednym z {STATUSES}")
    if note is not None and append is not None:
        fail("--note albo --append-note, nie oba")
    if status is None and title is None and note is None and append is None:
        fail("nic do zmiany: podaj --status, --title, --note albo --append-note")
    if title is not None and not title.strip():
        fail("pusty --title")
    plan = load(args.plan)
    item = find(plan, args.id)
    changes = []
    if status is not None:
        item["status"] = status
        changes.append(status)
    if title is not None:
        item["title"] = title.strip()
        changes.append("tytuł")
    if note is not None:
        item["note"] = note
        changes.append("notatka")
    if append is not None:
        item["note"] = f"{item['note']}{NOTE_SEP}{append}" if item.get("note") else append
        changes.append("notatka+")
    save(args.plan, plan)
    print(f"{args.id} -> {', '.join(changes)}")


def cmd_check(args, status):
    args.status, args.note, args.title, args.append_note = status, None, None, None
    cmd_set(args)


def cmd_next(args) -> None:
    path = getattr(args, "plan", None)
    if not path:
        flying = in_flight_files(default_plans_dir())
        if not flying:
            fail("brak planu w toku (open/active); podaj PLAN.json")
        path = str(flying[0])
    item = next_item(load(path))
    print(json.dumps({"plan": path, "item": item}, ensure_ascii=False, indent=2))


def cmd_add(args) -> None:
    plan = load(args.plan)
    if not args.item:
        fail("podaj co najmniej jeden --item 'ID|Tytuł'")
    ids = {i["id"] for i in plan["items"]}
    added = 0
    for raw in args.item:
        item_id, sep, title = raw.partition("|")
        if not sep or not title.strip() or not ID_RE.match(item_id) or item_id in ids:
            fail(f"zły --item (oczekiwano 'ID|Tytuł', ID unikalne jak 1, 1.2): {raw!r}")
        ids.add(item_id)
        plan["items"].append(
            {
                "id": item_id,
                "title": title.strip(),
                "status": args.status,
                "note": args.note or "",
            }
        )
        added += 1
    plan["items"].sort(key=lambda i: sort_key(i["id"]))
    save(args.plan, plan)
    print(f"dodano {added} elementów")


MARK = {"todo": " ", "partial": "~", "done": "x"}


def md_inline(text: str) -> str:
    """Owija typy generyczne w backticki; fragmenty już w backtickach bez zmian."""
    parts = text.split("`")
    for i in range(0, len(parts), 2):
        parts[i] = GENERIC_RE.sub(lambda m: f"`{m.group(0)}`", parts[i])
    return "`".join(parts)


def render_text(plan):
    status = PLAN_LABELS.get(plan.get("status", "open"), plan.get("status", "open"))
    lines = [f"# {md_inline(plan.get('title', 'Plan'))} (status: {status})", ""]
    done = 0
    for item in sorted(plan["items"], key=lambda i: sort_key(i["id"])):
        status = item["status"]
        done += status == "done"
        extra = f" — {md_inline(item['note'])}" if item.get("note") else ""
        lines.append(f"- [{MARK[status]}] **{item['id']}.** {md_inline(item['title'])}{extra}")
    lines += ["", f"Postęp: {done}/{len(plan['items'])} gotowych."]
    return "\n".join(lines) + "\n"


def cmd_delete(args) -> None:
    plan = load(args.plan)
    item = find(plan, args.id)
    plan["items"].remove(item)
    save(args.plan, plan)
    print(f"usunięto {args.id}")


def cmd_status(args) -> None:
    plan = load(args.plan)
    if args.to is None:
        print(plan.get("status", "open"))
        return
    if args.to not in PLAN_STATUSES:
        fail(f"status planu musi być jednym z {PLAN_STATUSES}")
    dest = Path(args.plan)
    if args.to == "closed" and not all_items_done(plan):
        fail("closed tylko gdy wszystkie podpunkty są done")
    refuse_second_in_flight(dest, args.to)
    plan["status"] = args.to
    save(args.plan, plan)
    print(f"plan -> {args.to}")


def cmd_hygiene(args) -> None:
    plans_dir = Path(args.dir) if args.dir else default_plans_dir()
    errors = hygiene_errors(plans_dir)
    if errors:
        for err in errors:
            print(f"plan.py: {err}", file=sys.stderr)
        sys.exit(1)
    print(f"hygiene OK ({plans_dir})")


def cmd_render(args):
    plan = load(args.plan)
    out = args.out or str(Path(args.plan).with_suffix(".md"))
    Path(out).write_text(render_text(plan), encoding="utf-8")
    print(f"zapisano {out}")


def cmd_selftest(_args):
    with tempfile.TemporaryDirectory() as tmp:
        plan_file = str(Path(tmp) / "01-demo.json")
        ns = argparse.Namespace(
            plan=plan_file, title="Demo", item=["1|A", "1.1|B"], force=False
        )
        cmd_create(ns)
        assert len(load(plan_file)["items"]) == 2
        ns = argparse.Namespace(
            plan=plan_file, id="1.1", status="partial", note="w toku"
        )
        cmd_set(ns)
        assert find(load(plan_file), "1.1")["status"] == "partial"
        ns = argparse.Namespace(plan=plan_file, id="1.1", status=None, note=None)
        cmd_check(ns, "done")
        assert find(load(plan_file), "1.1")["status"] == "done"
        md = render_text(load(plan_file))
        assert "- [x] **1.1.** B — w toku" in md and "Postęp: 1/2" in md
        ns = argparse.Namespace(plan=plan_file, id="1.1")
        cmd_delete(ns)
        assert [i["id"] for i in load(plan_file)["items"]] == ["1"]
        ns = argparse.Namespace(plan=plan_file, to=None)
        assert load(plan_file).get("status") == "open"
        ns = argparse.Namespace(plan=plan_file, to="active")
        cmd_status(ns)
        assert load(plan_file)["status"] == "active"
        md = render_text(load(plan_file))
        assert "(status: w trakcie)" in md
        ns = argparse.Namespace(plan=plan_file, item=["2|C"], status="todo", note="")
        cmd_add(ns)
        assert [i["id"] for i in load(plan_file)["items"]] == ["1", "2"]
        ns = argparse.Namespace(plan=plan_file, to="closed")
        try:
            cmd_status(ns)
            raise AssertionError("closed przy niedokończonych podpunktach")
        except SystemExit:
            pass
        catalog = Path(tmp) / "docs" / "plans"
        catalog.mkdir(parents=True)
        a = catalog / "01-one.json"
        b = catalog / "02-two.json"
        save(str(a), {"title": "A", "status": "open", "items": [
            {"id": "1", "title": "x", "status": "done", "note": ""}
        ]})
        save(str(b), {"title": "B", "status": "open", "items": [
            {"id": "1", "title": "y", "status": "todo", "note": ""}
        ]})
        errors = hygiene_errors(catalog)
        assert any("więcej niż jeden" in e for e in errors)
        save(str(b), {"title": "B", "status": "queued", "items": [
            {"id": "1", "title": "y", "status": "todo", "note": ""}
        ]})
        assert hygiene_errors(catalog) == []
        save(str(a), {"title": "A", "status": "closed", "items": [
            {"id": "1", "title": "x", "status": "todo", "note": ""}
        ]})
        assert any("closed" in e for e in hygiene_errors(catalog))
        save(str(a), {"title": "A", "status": "closed", "items": [
            {"id": "1", "title": "x", "status": "done", "note": ""},
            {"id": "1.1", "title": "y", "status": "todo", "note": ""},
        ]})
        assert any("1 done, a dzieci nie: 1.1" in e for e in hygiene_errors(catalog))
        assert (catalog / "02-two.md").read_text(encoding="utf-8") == render_text(
            load(str(b))
        )
        (catalog / "02-two.md").write_text("ręcznie\n", encoding="utf-8")
        assert any("nieaktualny" in e for e in hygiene_errors(catalog))
        (catalog / "02-two.md").unlink()
        assert any("brak 02-two.md" in e for e in hygiene_errors(catalog))
        plan_file = str(Path(tmp) / "03-edit.json")
        cmd_create(argparse.Namespace(
            plan=plan_file, title="E", item=["1|A", "1.1|B", "2|C"], force=False
        ))
        cmd_set(argparse.Namespace(plan=plan_file, id="1.1", title="B2"))
        cmd_set(argparse.Namespace(plan=plan_file, id="1.1", append_note="x"))
        cmd_set(argparse.Namespace(plan=plan_file, id="1.1", append_note="y"))
        item = find(load(plan_file), "1.1")
        assert (item["title"], item["note"], item["status"]) == ("B2", "x · y", "todo")
        for bad in ({}, {"note": "a", "append_note": "b"}, {"title": " "}):
            try:
                cmd_set(argparse.Namespace(plan=plan_file, id="1.1", **bad))
                raise AssertionError(f"set bez błędu: {bad}")
            except SystemExit:
                pass
        assert next_item(load(plan_file))["id"] == "1.1"
        cmd_check(argparse.Namespace(plan=plan_file, id="1.1"), "done")
        assert next_item(load(plan_file))["id"] == "1"
        assert md_inline("OpcodeTable<TCpu> i Func<A, B<C>>, a < b > c") == (
            "`OpcodeTable<TCpu>` i `Func<A, B<C>>`, a < b > c"
        )
        assert md_inline("już `Foo<T>` tu") == "już `Foo<T>` tu"
    print("selftest OK")


def main(argv=None):
    parser = argparse.ArgumentParser(
        description="Narzędzie planu zadań (JSON + render .md)."
    )
    sub = parser.add_subparsers(dest="cmd", required=True)

    p = sub.add_parser("create", help="utwórz plan z JSON/itemów")
    p.add_argument("plan")
    p.add_argument("--title", required=True)
    p.add_argument("--item", action="append", default=[])
    p.add_argument("--status", default="open", choices=PLAN_STATUSES)
    p.add_argument("--force", action="store_true")
    p.set_defaults(func=cmd_create)

    p = sub.add_parser("get", help="pobierz element(y) jako JSON")
    p.add_argument("plan")
    p.add_argument("id", nargs="?")
    p.set_defaults(func=cmd_get)

    p = sub.add_parser("set", help="ustaw status elementu")
    p.add_argument("plan")
    p.add_argument("id")
    p.add_argument("--status", choices=STATUSES)
    p.add_argument("--title")
    p.add_argument("--note")
    p.add_argument("--append-note", dest="append_note")
    p.set_defaults(func=cmd_set)

    p = sub.add_parser("next", help="pierwszy wykonalny element (JSON)")
    p.add_argument("plan", nargs="?")
    p.set_defaults(func=cmd_next)

    for name, status in (("check", "done"), ("uncheck", "todo")):
        p = sub.add_parser(name, help=f"oznacz jako {status}")
        p.add_argument("plan")
        p.add_argument("id")
        p.set_defaults(func=lambda a, s=status: cmd_check(a, s))

    p = sub.add_parser("delete", aliases=["rm"], help="usuń element z planu")
    p.add_argument("plan")
    p.add_argument("id")
    p.set_defaults(func=cmd_delete)

    p = sub.add_parser("add", help="dodaj elementy do planu")
    p.add_argument("plan")
    p.add_argument("--item", action="append", default=[], help="'ID|Tytuł'")
    p.add_argument("--status", default="todo", choices=STATUSES)
    p.add_argument("--note", default="")
    p.set_defaults(func=cmd_add)

    p = sub.add_parser(
        "status",
        help="pokaż lub ustaw status planu (open/active/queued/closed)",
    )
    p.add_argument("plan")
    p.add_argument("--to", choices=PLAN_STATUSES)
    p.set_defaults(func=cmd_status)

    p = sub.add_parser("render", help="wyrenderuj checklistę .md")
    p.add_argument("plan")
    p.add_argument("--out")
    p.set_defaults(func=cmd_render)

    p = sub.add_parser("hygiene", help="sprawdź księgę docs/plans")
    p.add_argument("dir", nargs="?")
    p.set_defaults(func=cmd_hygiene)

    p = sub.add_parser("selftest", help="wbudowany test narzędzia")
    p.set_defaults(func=cmd_selftest)

    args = parser.parse_args(argv)
    args.func(args)


if __name__ == "__main__":
    main()
