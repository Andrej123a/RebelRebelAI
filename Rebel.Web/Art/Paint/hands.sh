#!/bin/bash
# Paints the fingers the cast curl over the pictures they carry on the home page:
# Art/Hands/*.svg -> wwwroot/art/hands/*.webp (1000 x 150, the top strip of the sheet).
#   Art/Paint/hands.sh [key ...]      (mars, ziggy and jack when no key is given)
# Needs the same tools as paint.sh.
set -e
HERE="$(cd "$(dirname "$0")" && pwd)"
SITE="$HERE/../../wwwroot/art/hands"
WORK="$(mktemp -d)"
KEYS="${*:-mars ziggy jack}"
mkdir -p "$SITE"
cd "$WORK"
mkdir -p out
PAINT_DRAWINGS="$HERE/../Hands" python3 "$HERE/flat.py" $KEYS
node "$HERE/raster.js" svg/*.svg
for key in $KEYS; do
    PAINT_PLAIN=1 python3 "$HERE/painter.py" "$key"
    python3 -c "from PIL import Image; Image.open('out/$key.png').crop((0, 0, 1000, 150)).save('$SITE/$key.webp', 'WEBP', quality=82, method=6, alpha_quality=90)"
done
rm -rf "$WORK"
