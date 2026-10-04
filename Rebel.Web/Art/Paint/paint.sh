#!/bin/bash
# Paints the cast: Art/Looks/*.svg -> wwwroot/art/looks/*.webp (1000 x 1300).
#   Art/Paint/paint.sh [key ...]      (all nine when no key is given)
# Needs python3 with numpy, opencv-python-headless and pillow, and node with playwright.
set -e
HERE="$(cd "$(dirname "$0")" && pwd)"
SITE="$HERE/../../wwwroot/art/looks"
WORK="$(mktemp -d)"
KEYS="${*:-tom mars ziggy sane rebel jack duke pierrot prophet}"
cd "$WORK"
mkdir -p out
python3 "$HERE/flat.py" $KEYS
node "$HERE/raster.js" svg/*.svg
for key in $KEYS; do
    python3 "$HERE/painter.py" "$key"
    python3 -c "from PIL import Image; Image.open('out/$key.png').save('$SITE/$key.webp', 'WEBP', quality=80, method=6, alpha_quality=90)"
done
rm -rf "$WORK"
