# Cuts each painted look down to a head-and-shoulders portrait for the cast's calls and
# the navbar: wwwroot/art/looks/{key}.webp -> wwwroot/art/faces/{key}.webp (320 x 320).
#   python3 faces.py [key ...]
import os, sys
from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
LOOKS = os.path.join(HERE, '..', '..', 'wwwroot', 'art', 'looks')
FACES = os.path.join(HERE, '..', '..', 'wwwroot', 'art', 'faces')
SIZE = 320
# square boxes (x, y, side) in the 400 x 520 drawing: the head, a little of the shoulders
BOX = {
    'tom': (66, 70, 268),
    'mars': (72, 92, 256),
    'ziggy': (146, 0, 108),
    'sane': (62, 66, 276),
    'rebel': (58, 58, 284),
    'jack': (146, 6, 108),
    'duke': (92, 58, 250),
    'pierrot': (72, 16, 258),
    'prophet': (70, 90, 260),
}
S = 2.5
os.makedirs(FACES, exist_ok=True)
for key in sys.argv[1:] or BOX:
    x, y, side = BOX[key]
    look = Image.open(os.path.join(LOOKS, f'{key}.webp')).convert('RGBA')
    face = look.crop((round(x * S), round(y * S), round((x + side) * S), round((y + side) * S)))
    face = face.resize((SIZE, SIZE), Image.LANCZOS)
    face.save(os.path.join(FACES, f'{key}.webp'), 'WEBP', quality=82, method=6, alpha_quality=90)
    print(key, os.path.getsize(os.path.join(FACES, f'{key}.webp')))
