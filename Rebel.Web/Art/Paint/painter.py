# Paints a look: lights the flat drawing (form, cast shadows, a modelled face, warm
# skin, rim light from the space behind), then repaints it stroke by stroke with bristle
# brushes over a toned ground (Hertzmann's layered painterly rendering, loose in the big
# shapes and careful round the eyes and mouth), on linen, with a soft pencil line.
#   python3 painter.py key [out.png]     (run from the work folder paint.sh sets up)
import os, sys, math, numpy as np, cv2
from PIL import Image

KEY = sys.argv[1]
OUT = sys.argv[2] if len(sys.argv) > 2 else f'out/{KEY}.png'
RIM = {'tom': '#8fc4ff', 'mars': '#ff9a5c', 'ziggy': '#c2a4ff', 'sane': '#ffb070', 'rebel': '#9db8ff',
       'jack': '#8fe0ff', 'duke': '#f0dcb0', 'pierrot': '#c79cff', 'prophet': '#ffc070'}
# face boxes (x0, y0, x1, y1 in the 400 x 520 drawing); front faces share one template
FRONT = (134, 148, 265, 342)
FACES = {'mars': FRONT, 'sane': FRONT, 'rebel': FRONT, 'pierrot': (134, 150, 265, 342),
         'prophet': (135, 150, 264, 342), 'ziggy': (182, 43, 217, 88), 'jack': (182, 49, 217, 90)}
PROFILE = {'duke': (139, 116, 279, 265)}
S = 2.5
SEED = sum(map(ord, KEY))
rng = np.random.default_rng(SEED)
f32 = np.float32


def load(p):
    return np.asarray(Image.open(p).convert('RGBA')).astype(f32) / 255


def hexrgb(h):
    return np.array([int(h[i:i + 2], 16) for i in (1, 3, 5)], f32) / 255


col = load(f'svg/{KEY}-colour.png')
ink = load(f'svg/{KEY}-ink.png')
fx = load(f'svg/{KEY}-fx.png')
idp = np.asarray(Image.open(f'svg/{KEY}-ids.png').convert('RGBA')).astype(np.int32)
H, W = col.shape[:2]
A = col[..., 3].copy()
base = col[..., :3].copy()
T = np.where(idp[..., 3] > 127, idp[..., 0] + idp[..., 1] * 256, 0)
inside = (A > 0.5).astype(np.uint8)
yy, xx = np.mgrid[0:H, 0:W].astype(f32)

# ------------------------------------------------- what is a thing, what is painted on
ring_k = cv2.getStructuringElement(cv2.MORPH_ELLIPSE, (15, 15))
Tp = T.copy()
casters = []
for i in np.unique(T[T > 0]):
    m = (T == i)
    area = int(m.sum())
    m8 = m.astype(np.uint8)
    ring = (cv2.dilate(m8, ring_k) > 0) & ~m
    under = T[ring]
    lower = under[(under > 0) & (under < i)]
    host = 0
    if lower.size:
        vals, cnt = np.unique(lower, return_counts=True)
        host = vals[cnt.argmax()]
    bg = (under == 0).mean() if under.size else 1
    rim_px = cv2.countNonZero(cv2.morphologyEx(m8, cv2.MORPH_GRADIENT, np.ones((3, 3), np.uint8)))
    thin = area < 500 or area / max(1, rim_px) < 2.2
    host_share = (under == host).mean() if under.size and host else 0
    if host and bg < 0.05 and (host_share > 0.8 or thin):
        Tp[m] = Tp[T == host][0] if (T == host).any() else host   # painted on: lies flat on its host
    else:
        casters.append(i)

# ------------------------------------------------------------------- form and light
dS = cv2.distanceTransform(inside, cv2.DIST_L2, 5)
edges = np.zeros_like(inside)
edges[:-1] |= (Tp[:-1] != Tp[1:]).astype(np.uint8)
edges[:, :-1] |= (Tp[:, :-1] != Tp[:, 1:]).astype(np.uint8)
dR = cv2.distanceTransform((1 - edges) * inside, cv2.DIST_L2, 5)
# inflate the silhouette (blurred at three scales, no ridge down the middle) and let
# each separate thing swell a little on its own
fin = inside.astype(f32)
dome = (cv2.GaussianBlur(fin, (0, 0), 14) + cv2.GaussianBlur(fin, (0, 0), 34) + cv2.GaussianBlur(fin, (0, 0), 70)) / 3
h = 0.85 * dome + 0.15 * cv2.GaussianBlur(np.sqrt(np.minimum(dR, 40) / 40).astype(f32), (0, 0), 6)
h = h.astype(f32)
gx = cv2.Sobel(h, cv2.CV_32F, 1, 0, ksize=5) / 32
gy = cv2.Sobel(h, cv2.CV_32F, 0, 1, ksize=5) / 32
k = 68.0
n = np.dstack([-k * gx, -k * gy, np.ones_like(h)])
n /= np.linalg.norm(n, axis=2, keepdims=True)
L = np.array([-0.55, -0.62, 0.56], f32); L /= np.linalg.norm(L)
R = np.array([0.95, -0.15, 0.05], f32); R /= np.linalg.norm(R)
diffuse = np.clip((n @ L + 0.4) / 1.4, 0, 1)
rim = np.clip(n @ R, 0, 1) ** 2.2

shadow = np.zeros((H, W), f32)
for i in casters:
    caster = (T == i).astype(f32)
    soft = cv2.GaussianBlur(cv2.warpAffine(caster, np.float32([[1, 0, 9], [0, 1, 15]]), (W, H)), (0, 0), 11)
    tight = cv2.GaussianBlur(cv2.warpAffine(caster, np.float32([[1, 0, 2.5], [0, 1, 4]]), (W, H)), (0, 0), 3)
    below = ((Tp < i) & (Tp > 0) & (T != i)).astype(f32)
    shadow = np.maximum(shadow, np.maximum(0.75 * soft, 0.6 * tight) * below)
edge_dark = np.clip(1 - dS / 55, 0, 1) ** 1.5

# the face, modelled: sockets, brow, nose, cheekbones and hollows, jaw, temples
model = np.zeros((H, W), f32)
blush = np.zeros((H, W), f32)
detail = np.zeros((H, W), f32)


def blobs(box, spec, into):
    x0, y0, x1, y1 = [v * S for v in box]
    cx, w, hh = (x0 + x1) / 2, (x1 - x0), (y1 - y0)
    u = (xx - cx) / (w / 2)
    v = (yy - y0) / hh
    for (u0, v0, ru, rv, amt) in spec:
        into += amt * np.exp(-((u - u0) / ru) ** 2 - ((v - v0) / rv) ** 2)


FRONT_MODEL = [
    (-0.38, 0.47, 0.30, 0.085, -0.17), (0.38, 0.47, 0.30, 0.085, -0.24),
    (-0.16, 0.48, 0.09, 0.07, -0.10), (0.16, 0.48, 0.09, 0.07, -0.14),
    (-0.35, 0.385, 0.28, 0.04, 0.09), (0.35, 0.385, 0.28, 0.04, 0.04),
    (-0.2, 0.2, 0.42, 0.13, 0.10),
    (-0.03, 0.58, 0.05, 0.12, 0.13), (0.1, 0.62, 0.07, 0.10, -0.17),
    (0.03, 0.725, 0.13, 0.022, -0.24), (-0.015, 0.685, 0.05, 0.03, 0.11),
    (-0.5, 0.58, 0.2, 0.07, 0.11), (0.5, 0.58, 0.2, 0.07, 0.04),
    (-0.62, 0.73, 0.2, 0.1, -0.09), (0.6, 0.73, 0.22, 0.11, -0.17),
    (0.0, 0.845, 0.15, 0.025, -0.15), (-0.04, 0.93, 0.13, 0.04, 0.07),
    (0.82, 0.7, 0.25, 0.32, -0.16), (-0.86, 0.7, 0.2, 0.3, -0.07),
    (-0.86, 0.33, 0.2, 0.15, -0.08), (0.86, 0.33, 0.2, 0.15, -0.13),
]
FRONT_BLUSH = [(-0.48, 0.66, 0.22, 0.09, 1.0), (0.48, 0.66, 0.22, 0.09, 0.9), (0.0, 0.68, 0.07, 0.04, 0.7)]
FRONT_DETAIL = [(-0.36, 0.47, 0.34, 0.12, 1), (0.36, 0.47, 0.34, 0.12, 1), (0.0, 0.64, 0.14, 0.12, 0.8), (0.0, 0.8, 0.3, 0.08, 1)]
# profile, looking left: the front of the face is u < 0
PROFILE_MODEL = [
    (-0.55, 0.42, 0.2, 0.08, -0.2), (-0.62, 0.34, 0.25, 0.04, 0.09), (-0.4, 0.15, 0.4, 0.15, 0.08),
    (-0.42, 0.56, 0.22, 0.07, 0.12), (-0.38, 0.7, 0.22, 0.09, -0.15), (0.2, 0.85, 0.5, 0.15, -0.18),
    (-0.05, 0.3, 0.2, 0.18, -0.08), (-0.78, 0.72, 0.1, 0.03, -0.12)]
PROFILE_BLUSH = [(-0.45, 0.62, 0.2, 0.09, 1.0), (-0.86, 0.6, 0.06, 0.05, 0.6)]
PROFILE_DETAIL = [(-0.6, 0.45, 0.25, 0.12, 1), (-0.8, 0.75, 0.15, 0.12, 1)]

face_area = np.zeros((H, W), f32)
if (KEY in FACES or KEY in PROFILE) and not os.environ.get('PAINT_PLAIN'):
    box = FACES.get(KEY) or PROFILE[KEY]
    spec = (FRONT_MODEL, FRONT_BLUSH, FRONT_DETAIL) if KEY in FACES else (PROFILE_MODEL, PROFILE_BLUSH, PROFILE_DETAIL)
    blobs(box, spec[0], model)
    blobs(box, spec[1], blush)
    blobs(box, spec[2], detail)
    fid = Tp[int((box[1] + box[3]) / 2 * S), int((box[0] + box[2]) / 2 * S)]
    face_area = cv2.GaussianBlur((Tp == fid).astype(f32), (0, 0), 3)
    model *= face_area
    blush *= face_area
    detail *= face_area

# skin is alive: warm where the light turns, cool in the shadows
hsv = cv2.cvtColor(base, cv2.COLOR_RGB2HSV)
skin = ((hsv[..., 0] > 12) & (hsv[..., 0] < 42) & (hsv[..., 1] > 0.09) & (hsv[..., 1] < 0.4) & (hsv[..., 2] > 0.78)).astype(f32)
skin = np.maximum(skin, face_area * (hsv[..., 1] < 0.4))
skin = cv2.GaussianBlur(skin, (0, 0), 2)

lin = base ** 2.2
d = np.clip(diffuse * (1 - 0.8 * shadow) + model * 1.45, 0, 1.2)
key_col = np.array([1.0, 0.92, 0.80], f32) * 1.06
amb_col = np.array([0.27, 0.25, 0.32], f32)
rim_col = hexrgb(RIM[KEY]) ** 2.2
lit = lin * (amb_col + key_col * d[..., None])
dc1 = np.clip(d, 0, 1)
turn = 4 * dc1 * (1 - dc1)
lit *= (1 + skin[..., None] * (turn[..., None] * np.array([0.12, -0.04, -0.14], f32) +
                               (np.clip(0.45 - d, 0, 1) * 2)[..., None] * np.array([-0.02, -0.03, 0.05], f32)))
blush_col = np.array([0.92, 0.42, 0.42], f32) ** 2.2
lit = lit * (1 - 0.16 * blush[..., None]) + blush_col * lit.mean(axis=2, keepdims=True) * 1.6 * 0.16 * blush[..., None]
lit += lin.mean(axis=2, keepdims=True) * 0.25 * rim_col * rim[..., None] + 0.22 * rim_col * (rim ** 3)[..., None]
lit *= (1 - 0.35 * edge_dark)[..., None]
lit *= (1 - 0.22 * shadow)[..., None]
shaded = np.clip(lit, 0, 1) ** (1 / 2.2)
shaded = shaded * (1 - fx[..., 3:]) + fx[..., :3] * fx[..., 3:]
A = np.maximum(A, fx[..., 3])

# where the eye goes: features, makeup edges, the drawing's lines
dec_edges = ((T != Tp) & (A > 0.5)).astype(np.uint8)
dec_edges = cv2.morphologyEx(dec_edges, cv2.MORPH_GRADIENT, np.ones((3, 3), np.uint8))
lines = (ink[..., 3] > 0.3).astype(np.uint8)
focus = np.maximum(detail, cv2.GaussianBlur(cv2.dilate(np.maximum(dec_edges, lines), np.ones((9, 9), np.uint8)).astype(f32), (0, 0), 4) * 0.8)
focus = np.clip(focus, 0, 1)

# ------------------------------------------------------------------------ painter


def blur_premul(img, a, s):
    if s < 0.3:
        return img.copy(), a.copy()
    num = cv2.GaussianBlur(img * a[..., None], (0, 0), s)
    den = cv2.GaussianBlur(a, (0, 0), s)
    return num / np.maximum(den[..., None], 1e-4), den


def make_dabs(r, patterns=6, angles=16):
    size = int(math.ceil(r * 2.2)) * 2 + 3
    c = size // 2
    gy_, gx_ = np.mgrid[0:size, 0:size].astype(f32) - c
    bank = []
    for p in range(patterns):
        pr = np.random.default_rng(SEED * 31 + p * 7 + int(r * 10))
        bristles = max(4, int(r * 1.6))
        prof = np.convolve(pr.random(bristles + 3), [0.25, 0.5, 0.25], 'same')
        ragged = pr.random(bristles + 3) * 0.18
        rows = []
        for a in range(angles):
            th = math.pi * a / angles
            u = (gx_ * math.cos(th) + gy_ * math.sin(th)) / max(r * 0.55, 0.6)
            v = (-gx_ * math.sin(th) + gy_ * math.cos(th)) / r
            vi = np.clip((v + 1) / 2 * (bristles + 1), 0, bristles + 2).astype(int)
            b = 0.45 + 0.55 * prof[vi]
            edge = np.clip((1 - np.abs(v) - ragged[vi]) / 0.18, 0, 1)
            along = np.exp(-u * u * 1.6)
            rows.append(((edge * b * along).astype(f32), (prof[vi] - 0.5).astype(f32)))
        bank.append(rows)
    return bank, c


dRs = cv2.GaussianBlur(dR, (0, 0), 3)
edge_ang = np.arctan2(cv2.Sobel(dRs, cv2.CV_32F, 0, 1, ksize=5), cv2.Sobel(dRs, cv2.CV_32F, 1, 0, ksize=5)) + math.pi / 2
hand = cv2.resize(cv2.GaussianBlur(rng.normal(0, 1, (H // 40, W // 40)).astype(f32), (0, 0), 1.2), (W, H))
hand_ang = math.radians(-58) + hand * math.radians(22)


def flow_field(img, r):
    lum = img @ np.array([0.299, 0.587, 0.114], f32)
    gx = cv2.Sobel(lum, cv2.CV_32F, 1, 0, ksize=3)
    gy = cv2.Sobel(lum, cv2.CV_32F, 0, 1, ksize=3)
    s = max(3.0, r * 1.5)
    jxx = cv2.GaussianBlur(gx * gx, (0, 0), s)
    jxy = cv2.GaussianBlur(gx * gy, (0, 0), s)
    jyy = cv2.GaussianBlur(gy * gy, (0, 0), s)
    theta = 0.5 * np.arctan2(2 * jxy, jxx - jyy) + math.pi / 2
    tr = jxx + jyy
    det = np.sqrt((jxx - jyy) ** 2 + 4 * jxy ** 2)
    coh = np.where(tr > 1e-6, det / np.maximum(tr, 1e-6), 0)
    strength = np.clip(np.sqrt(tr) * 6, 0, 1) * np.clip(coh * 1.6, 0, 1)
    near = np.clip(1 - dR / 32, 0, 1) ** 0.7
    vx = np.cos(2 * hand_ang) * (1 - near) + np.cos(2 * edge_ang) * near
    vy = np.sin(2 * hand_ang) * (1 - near) + np.sin(2 * edge_ang) * near
    vx = vx * (1 - strength) + np.cos(2 * theta) * strength
    vy = vy * (1 - strength) + np.sin(2 * theta) * strength
    return (0.5 * np.arctan2(vy, vx)).astype(f32)


def jitter(c, amt):
    hsv_ = cv2.cvtColor(np.clip(c, 0, 1).reshape(1, 1, 3).astype(f32), cv2.COLOR_RGB2HSV)[0, 0]
    hsv_[0] = (hsv_[0] + rng.normal(0, 60 * amt)) % 360
    hsv_[1] = np.clip(hsv_[1] * (1 + rng.normal(0, amt * 2)), 0, 1)
    hsv_[2] = np.clip(hsv_[2] * (1 + rng.normal(0, amt * 1.5)), 0, 1)
    return cv2.cvtColor(hsv_.reshape(1, 1, 3), cv2.COLOR_HSV2RGB)[0, 0]


# a toned ground, warm umber, under the colour
ground_a = cv2.GaussianBlur(cv2.dilate(A, cv2.getStructuringElement(cv2.MORPH_ELLIPSE, (9, 9))), (0, 0), 1.5)
gref, _ = blur_premul(shaded, A, 18)
canvas = (gref * 0.55 + np.array([0.42, 0.22, 0.13], f32) * 0.45).astype(f32)
cA = ground_a.astype(f32)
# radius, colour tolerance, max length, opacity, jitter, threshold (loose), threshold (focus)
LAYERS = [(20, 0.12, 8, 0.9, 0.035, 0.0, 0.0), (10, 0.09, 10, 0.9, 0.03, 0.10, 0.06),
          (5, 0.07, 10, 0.9, 0.025, 0.11, 0.045), (2.4, 0.05, 7, 0.92, 0.015, 0.13, 0.035),
          (1.3, 0.04, 4, 0.95, 0.01, 9.0, 0.03)]

for li, (r, tol, max_len, opacity, jit, th_loose, th_focus) in enumerate(LAYERS):
    ref, refA = blur_premul(shaded, A, 0.6 * r if r > 2 else 0.5)
    ang = flow_field(ref, r)
    dabs, dc = make_dabs(r)
    nang = len(dabs[0])
    g = max(2, int(round(r * 0.9)))
    diff = np.linalg.norm(canvas - ref, axis=2)
    diff = np.where(cA < 0.6, 2.0, diff) if li else np.full_like(diff, 2.0)
    Hc, Wc = H // g, W // g
    D = diff[:Hc * g, :Wc * g].reshape(Hc, g, Wc, g).transpose(0, 2, 1, 3).reshape(Hc, Wc, g * g)
    Am = refA[:Hc * g, :Wc * g].reshape(Hc, g, Wc, g).mean(axis=(1, 3))
    Fm = focus[:Hc * g, :Wc * g].reshape(Hc, g, Wc, g).mean(axis=(1, 3))
    err = D.mean(axis=2)
    arg = D.argmax(axis=2)
    thr = th_loose * (1 - Fm) + th_focus * Fm
    cy, cx = np.nonzero((err > thr) & (Am > 0.5))
    order = rng.permutation(len(cy))
    strokes = 0
    for o in order:
        by, bx = cy[o], cx[o]
        a = arg[by, bx]
        y, x = by * g + a // g, bx * g + a % g
        c = ref[y, x].copy()
        paint = jitter(c, jit * (1 - 0.6 * focus[y, x]))
        ml = max(2, int(max_len * (1 - 0.6 * focus[y, x])))
        pts = [(x, y)]
        px, py = float(x), float(y)
        pdx = pdy = 0.0
        step = r * 0.9
        for s in range(ml):
            t = ang[int(py), int(px)]
            dx, dy = math.cos(t), math.sin(t)
            if s and dx * pdx + dy * pdy < 0:
                dx, dy = -dx, -dy
            if s:
                dx, dy = 0.65 * dx + 0.35 * pdx, 0.65 * dy + 0.35 * pdy
                l = math.hypot(dx, dy) or 1
                dx, dy = dx / l, dy / l
            nx, ny = px + step * dx, py + step * dy
            if not (2 <= nx < W - 2 and 2 <= ny < H - 2):
                break
            iy, ix = int(ny), int(nx)
            if refA[iy, ix] < 0.4:
                break
            here = ref[iy, ix]
            dc_ = float(np.abs(here - c).sum())
            if dc_ > tol * 3:
                break
            if s >= 2 and dc_ > float(np.abs(here - canvas[iy, ix]).sum()) and cA[iy, ix] > 0.9:
                break
            pts.append((nx, ny))
            px, py, pdx, pdy = nx, ny, dx, dy
        xs = [p[0] for p in pts]; ys = [p[1] for p in pts]
        x0 = int(max(0, min(xs) - dc - 1)); x1 = int(min(W, max(xs) + dc + 2))
        y0 = int(max(0, min(ys) - dc - 1)); y1 = int(min(H, max(ys) + dc + 2))
        acc = np.zeros((y1 - y0, x1 - x0), f32)
        tone = np.zeros_like(acc)
        pat = dabs[rng.integers(len(dabs))]
        total = sum(math.hypot(pts[i + 1][0] - pts[i][0], pts[i + 1][1] - pts[i][1]) for i in range(len(pts) - 1))
        if total < 1:
            seg = [(pts[0][0], pts[0][1], ang[int(pts[0][1]), int(pts[0][0])], 0.5)]
        else:
            seg = []
            run = 0.0
            sp = max(0.5, r * 0.28)
            for i in range(len(pts) - 1):
                (ax, ay), (bx2, by2) = pts[i], pts[i + 1]
                l = math.hypot(bx2 - ax, by2 - ay)
                th = math.atan2(by2 - ay, bx2 - ax)
                m = max(1, int(l / sp))
                for j in range(m):
                    f = j / m
                    seg.append((ax + (bx2 - ax) * f, ay + (by2 - ay) * f, th, (run + l * f) / total))
                run += l
            seg.append((pts[-1][0], pts[-1][1], seg[-1][2], 1.0))
        for (sx, sy, th, tt) in seg:
            ai = int(round((th % math.pi) / math.pi * nang)) % nang
            dab, dt = pat[ai]
            press = min(1.0, 0.45 + tt * 4) * (1 - 0.45 * tt ** 1.5)
            ox, oy = int(round(sx)) - dc - x0, int(round(sy)) - dc - y0
            sy0, sx0 = max(0, oy), max(0, ox)
            sy1, sx1 = min(acc.shape[0], oy + dab.shape[0]), min(acc.shape[1], ox + dab.shape[1])
            if sy1 <= sy0 or sx1 <= sx0:
                continue
            dsl = dab[sy0 - oy:sy1 - oy, sx0 - ox:sx1 - ox] * press
            tsl = dt[sy0 - oy:sy1 - oy, sx0 - ox:sx1 - ox]
            cur = acc[sy0:sy1, sx0:sx1]
            more = dsl > cur
            acc[sy0:sy1, sx0:sx1] = np.where(more, dsl, cur)
            tone[sy0:sy1, sx0:sx1] = np.where(more, tsl, tone[sy0:sy1, sx0:sx1])
        a = np.clip(acc * 1.25, 0, 1) * opacity
        colr = paint[None, None, :] * (1 + 0.16 * tone[..., None])
        sl = (slice(y0, y1), slice(x0, x1))
        canvas[sl] = canvas[sl] * (1 - a[..., None]) + colr * a[..., None]
        cA[sl] = cA[sl] + a * (1 - cA[sl])
        strokes += 1
    print(KEY, 'layer', r, 'strokes', strokes, flush=True)

# ------------------------------------------------------------- pencil, linen, out
# the drawing's own lines and inked shapes, laid in last with a fine brush: wobbling a
# little, pressure that comes and goes, dry at the edges; dark lines take some of the
# colour under them, coloured ones keep theirs
ia = ink[..., 3]
irgb = ink[..., :3]
wob = [cv2.resize(cv2.GaussianBlur(rng.normal(0, 1, (H // 24, W // 24)).astype(f32), (0, 0), 1.2), (W, H)) * 2.0 for _ in range(4)]
line = cv2.remap(ia, xx + wob[0], yy + wob[1], cv2.INTER_LINEAR)
lrgb = cv2.remap(irgb, xx + wob[0], yy + wob[1], cv2.INTER_LINEAR)
dry = cv2.GaussianBlur(rng.random((H, W)).astype(f32), (0, 0), 1.4)
dry = np.clip((dry - dry.mean()) / dry.std() * 0.5 + 0.5, 0, 1)
core = cv2.GaussianBlur(line, (0, 0), 1.6)
pressure = cv2.resize(cv2.GaussianBlur(rng.random((H // 22, W // 22)).astype(f32), (0, 0), 1.3), (W, H))
pressure = np.clip(0.78 + (pressure - 0.5) * 2.4, 0.42, 1)
line = np.clip(line * pressure * np.clip(0.55 + core * 0.6 + dry * 0.25, 0, 1), 0, 1)
line = cv2.GaussianBlur(line, (0, 0), 0.55)
dark = (lrgb @ np.array([0.299, 0.587, 0.114], f32) < 0.3)[..., None]
lcol = np.where(dark, lrgb * 0.9 + canvas * 0.07 + np.array([0.02, 0.01, 0.0], f32), lrgb)
lcol = lcol * (1 + 0.12 * (cv2.GaussianBlur(rng.normal(0, 1, (H, W)).astype(f32), (0, 0), 2.5)[..., None]))
canvas = canvas * (1 - 0.95 * line[..., None]) + np.clip(lcol, 0, 1) * 0.95 * line[..., None]
cA = np.maximum(cA, line * 0.9 * (cA > 0.2))

phase = cv2.resize(cv2.GaussianBlur(rng.normal(0, 1, (H // 50, W // 50)).astype(f32), (0, 0), 1), (W, H)) * 2
weave = np.sin(xx * 2 * math.pi / 5.2 + phase) * np.sin(yy * 2 * math.pi / 5.6 - phase)
fib = cv2.GaussianBlur(rng.normal(0, 1, (H, W)).astype(f32), (0, 0), 1.1)
mottle = cv2.resize(cv2.GaussianBlur(rng.normal(0, 1, (H // 16, W // 16)).astype(f32), (0, 0), 2), (W, H))
canvas *= (1 + 0.025 * weave + 0.03 * fib + 0.045 * mottle)[..., None]
canvas = np.clip(canvas, 0, 1)
sil = cv2.remap(A, xx + wob[1] * 0.8, yy + wob[2] * 0.8, cv2.INTER_LINEAR)
limit = cv2.GaussianBlur(cv2.dilate(sil, cv2.getStructuringElement(cv2.MORPH_ELLIPSE, (7, 7))), (0, 0), 1.2)
alpha = np.clip(np.minimum(cA * 1.04, limit), 0, 1)
alpha = np.where(alpha < 0.04, 0, alpha)
Image.fromarray((np.dstack([canvas, alpha]) * 255 + 0.5).astype(np.uint8), 'RGBA').save(OUT)
Image.fromarray((np.dstack([shaded, A]) * 255 + 0.5).astype(np.uint8), 'RGBA').save(OUT.replace('.png', '-lit.png'))
print('saved', OUT)
