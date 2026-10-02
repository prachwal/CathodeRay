#!/bin/bash
# Siatka porównań listingów (plan 41, poz. 4): generuje listingi (cc --listing)
# wszystkich próbek samples/minic/??_.c na celach stub 6502 65c02 nes 6510 z80 8080 6800
# dla bieżącego drzewa i dla commita bazowego (git worktree), pokazuje diff i sumy CODE per cel.
# Dla refaktoru (plan 41) diff musi być pusty; w planie 42 diff przeglądany ręcznie.
#
# Użycie: tools/listing-diff.sh [BASE_COMMIT] [OUT_DIR]
#   BASE_COMMIT domyślnie HEAD, OUT_DIR domyślnie /tmp/listing-diff.
# Zwraca 0 gdy brak różnic, 1 gdy są (do bramek). Nie commituje baseline
# (generowany z commita bazowego, więc nie starzeje się).
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
BASE="${1:-HEAD}"
OUT="${2:-/tmp/listing-diff}"
CPUS="stub 6502 65c02 nes 6510 z80 8080 6800"
INCDIR="$ROOT/samples/minic"

build_dll() {
    dotnet build "$1/src/CathodeRay.Cli/CathodeRay.Cli.csproj" -v q --nologo >&2
    ls "$1"/src/CathodeRay.Cli/bin/Debug/*/cathode.dll | head -n 1
}

gen_tree() {
    local dll="$1" dest="$2"
    mkdir -p "$dest"
    : > "$dest/failures.txt"
    export DLL="$dll" DEST="$dest" INCDIR
    gen_one() {
        local sample="$1" cpu="$2"
        local base
        base="$(basename "$sample" .c)"
        local out="$DEST/$base.$cpu"
        if ! dotnet "$DLL" cc "$sample" --cpu "$cpu" -o "$out.bin" --listing "$out.lst" --incdir "$INCDIR" --stats > "$out.stats" 2>"$out.err"; then
            echo "FAIL $base $cpu" >> "$DEST/failures.txt"
        fi
    }
    export -f gen_one
    for sample in "$ROOT"/samples/minic/??_*.c; do
        for cpu in $CPUS; do
            echo "$sample $cpu"
        done
    done | xargs -P8 -n2 bash -c 'gen_one "$@"' _
}

sizes() {
    local dest="$1"
    for cpu in $CPUS; do
        local total
        total=$(grep -h -o 'CODE [0-9]*' "$dest"/*."$cpu".stats 2>/dev/null | awk '{s+=$2} END {print s+0}' || true)
        echo "$cpu ${total:-0}"
    done
}

BASETREE="$OUT/base-tree"
if git -C "$ROOT" worktree list --porcelain 2>/dev/null | grep -q "^worktree $BASETREE$"; then
    git -C "$ROOT" worktree remove --force "$BASETREE"
fi
rm -rf "$BASETREE" "$OUT/current" "$OUT/base"
git -C "$ROOT" worktree add --detach "$BASETREE" "$BASE" >/dev/null

echo "== build current =="
CURRENT_DLL="$(build_dll "$ROOT")"
echo "== build base ($BASE) =="
BASE_DLL="$(build_dll "$BASETREE")"
echo "== generate current =="
gen_tree "$CURRENT_DLL" "$OUT/current"
echo "== generate base =="
gen_tree "$BASE_DLL" "$OUT/base"

echo "== failures =="
cat "$OUT"/current/failures.txt "$OUT"/base/failures.txt
if [ -s "$OUT/current/failures.txt" ] || [ -s "$OUT/base/failures.txt" ]; then
    echo "COMPILATION FAILURES — grid incomplete"
    exit 1
fi
echo "== CODE per target (base vs current) =="
paste <(sizes "$OUT/base") <(sizes "$OUT/current" | awk '{print $2}') | awk '{d=$3-$2; printf "%-6s %8d %8d %+d\n", $1, $2, $3, d}'
echo "== diff =="
if diff -r --brief "$OUT/base" "$OUT/current" --exclude='*.bin' --exclude='*.stats' --exclude='*.err' --exclude='failures.txt' >"$OUT/brief.txt"; then
    echo "IDENTICAL instruction streams"
    bin_diff=0
    diff -rq "$OUT/base" "$OUT/current" --exclude='*.lst' --exclude='*.stats' --exclude='*.err' --exclude='failures.txt' >"$OUT/bin-diff.txt" || bin_diff=1
    exit "$bin_diff"
else
    cat "$OUT/brief.txt"
    diff -r "$OUT/base" "$OUT/current" --exclude='*.bin' --exclude='*.stats' --exclude='*.err' --exclude='failures.txt' >"$OUT/diff.txt" || true
    head -n 100 "$OUT/diff.txt"
    echo "... (full diff in $OUT/diff.txt)"
    exit 1
fi
