"""Sprawdza względne linki w *.md (po reorganizacji docs/). Kod wyjścia 1 = są martwe."""
import re
import subprocess
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
LINK = re.compile(r"\]\(([^)#\s]+)(?:#[^)]*)?\)")


def dead_links() -> list[str]:
    """Zwraca martwe linki względne w *.md repo jako `plik:linia: cel`."""
    bad = []
    tracked = subprocess.run(["git", "ls-files", "-co", "--exclude-standard", "*.md"], cwd=ROOT, capture_output=True, text=True, check=True).stdout.split("\n")
    for md in (ROOT / name for name in tracked if name):
        for n, line in enumerate(md.read_text(encoding="utf8").splitlines(), 1):
            for target in LINK.findall(line):
                # tylko ścieżki: pomija URL-e i wyrażenia kodu typu `tab[i](x)`
                if re.match(r"\w+:", target) or not re.search(r"[/.]", target):
                    continue
                if not (md.parent / target).resolve().exists():
                    bad.append(f"{md.relative_to(ROOT)}:{n}: {target}")
    return bad


if __name__ == "__main__":
    dead = dead_links()
    print("\n".join(dead) or "OK")
    sys.exit(1 if dead else 0)
