#!/bin/bash
# Paints the craft: Art/Ships/*.svg -> wwwroot/art/ships/*.webp (1000 x 1300), like the cast
# but without the face model (Major Tom's tin can on the booking page).
#   Art/Paint/ships.sh [key ...]      (tincan when no key is given)
# Needs the same tools as paint.sh.
set -e
HERE="$(cd "$(dirname "$0")" && pwd)"
SITE="$HERE/../../wwwroot/art/ships"
WORK="$(mktemp -d)"
KEYS="${*:-tincan}"
mkdir -p "$SITE"
cd "$WORK"
mkdir -p out
PAINT_DRAWINGS="$HERE/../Ships" python3 "$HERE/flat.py" $KEYS
node "$HERE/raster.js" svg/*.svg
for key in $KEYS; do
    PAINT_PLAIN=1 python3 "$HERE/painter.py" "$key"
    python3 -c "from PIL import Image; Image.open('out/$key.png').save('$SITE/$key.webp', 'WEBP', quality=80, method=6, alpha_quality=90)"
done
rm -rf "$WORK"
