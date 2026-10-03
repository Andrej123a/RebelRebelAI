// The night sky behind every public page, drawn on one fixed canvas:
// - a red nebula and the Milky Way, painted once and panned slowly down the page,
// - three depths of stars that twinkle and drift with the scroll and the pointer,
// - falling stars all night long, now and then a whole shower,
// - a flare wherever someone clicks the sky,
// - Ziggy's stardust trailing the pointer inside [data-rs-stardust],
// - "Tin can": a satellite crossing now and then, tagged by Ground Control,
// - a warp-speed starfield behind the countdown preloader.
// The sky is seeded, so it is the same on every visit. With reduced motion it is
// painted once and holds still; without JS the canvas keeps its CSS starfield.
(() => {
    const canvas = document.querySelector("[data-rs-sky]");
    const ctx = canvas && canvas.getContext ? canvas.getContext("2d") : null;

    if (!ctx) {
        return;
    }

    const root = document.documentElement;
    const reducedMotion = window.matchMedia("(prefers-reduced-motion: reduce)").matches;
    const finePointer = window.matchMedia("(pointer: fine)").matches;
    const TAU = Math.PI * 2;

    // mulberry32: a small seeded random, so the nebula never reshuffles.
    const seeded = (seed) => () => {
        seed = (seed + 0x6d2b79f5) | 0;
        let t = Math.imul(seed ^ (seed >>> 15), 1 | seed);
        t = (t + Math.imul(t ^ (t >>> 7), 61 | t)) ^ t;
        return ((t ^ (t >>> 14)) >>> 0) / 4294967296;
    };

    const gauss = (rand) => Math.sqrt(-2 * Math.log(1 - rand())) * Math.cos(TAU * rand());
    const pick = (rand, weighted) => {
        let roll = rand();

        for (const [value, weight] of weighted) {
            roll -= weight;

            if (roll <= 0) {
                return value;
            }
        }

        return weighted[weighted.length - 1][0];
    };

    const rgba = ([r, g, b], a) => `rgba(${r}, ${g}, ${b}, ${a})`;

    // Real star colours, hot blue to cool orange, plus Ziggy's gold.
    const STAR_COLOURS = [
        [[155, 176, 255], 0.08],
        [[202, 215, 255], 0.2],
        [[248, 247, 255], 0.3],
        [[255, 244, 234], 0.2],
        [[255, 226, 180], 0.12],
        [[255, 196, 120], 0.05],
        [[255, 216, 106], 0.05]
    ];

    const RED = [187, 21, 11];
    const HOT = [226, 64, 26];
    const DEEP = [125, 13, 6];
    const BLOOD = [96, 10, 8];
    const GOLD = [242, 194, 58];
    const BLUE = [154, 211, 240];
    const CREAM = [255, 241, 220];
    const MILK = [255, 232, 214];

    const NEBULA_COLOURS = [[RED, 0.44], [HOT, 0.16], [DEEP, 0.22], [BLOOD, 0.1], [GOLD, 0.05], [BLUE, 0.03]];
    const METEOR_COLOURS = [[CREAM, 0.68], [[255, 216, 106], 0.16], [BLUE, 0.1], [HOT, 0.06]];
    const DUST_COLOURS = [[GOLD, 0.46], [[255, 216, 106], 0.22], [CREAM, 0.22], [BLUE, 0.1]];

    // Far stars are many and faint, near stars few and bright.
    const LAYERS = [
        { area: 1500, size: [0.3, 0.85], scroll: 0.02, pointer: 3 },
        { area: 6500, size: [0.55, 1.4], scroll: 0.05, pointer: 7 },
        { area: 22000, size: [0.9, 2.3], scroll: 0.1, pointer: 14 }
    ];

    // The nebula is painted taller than the screen and pans across it once, top to bottom of the page.
    const DEEP_SPAN = 1.45;
    const DEEP_PAD = 28;

    // ---------- glow sprites, one per colour ----------

    const spriteFor = (() => {
        const cache = new Map();

        return (colour) => {
            const key = colour.join();

            if (!cache.has(key)) {
                const sprite = document.createElement("canvas");
                sprite.width = sprite.height = 64;
                const g = sprite.getContext("2d");
                const grad = g.createRadialGradient(32, 32, 0, 32, 32, 32);
                grad.addColorStop(0, rgba(colour, 1));
                grad.addColorStop(0.07, rgba(colour, 0.95));
                grad.addColorStop(0.18, rgba(colour, 0.32));
                grad.addColorStop(0.42, rgba(colour, 0.07));
                grad.addColorStop(1, rgba(colour, 0));
                g.fillStyle = grad;
                g.fillRect(0, 0, 64, 64);
                cache.set(key, sprite);
            }

            return cache.get(key);
        };
    })();

    // ---------- state ----------

    let W = 0;
    let H = 0;
    let dpr = 1;
    let docH = 0;
    let deep = null;
    let layers = [];
    let brights = [];

    const pointer = { x: 0, y: 0, tx: 0, ty: 0 };
    const meteors = [];
    const queued = [];
    const dust = [];
    const flares = [];
    let satellite = null;
    let nextMeteor = 1.2;
    let nextShower = 9;
    let nextSatellite = 4;
    const live = Math.random;

    // ---------- the deep sky: nebula and Milky Way ----------

    const blob = (g, x, y, r, colour, a) => {
        const grad = g.createRadialGradient(x, y, 0, x, y, r);
        grad.addColorStop(0, rgba(colour, a));
        grad.addColorStop(0.45, rgba(colour, a * 0.5));
        grad.addColorStop(1, rgba(colour, 0));
        g.fillStyle = grad;
        g.fillRect(x - r, y - r, r * 2, r * 2);
    };

    // A wandering line of small blobs: the threads and dark lanes in a nebula.
    const filament = (g, rand, x, y, steps, step, size, colourFor, alphaFor) => {
        let angle = rand() * TAU;

        for (let i = 0; i < steps; i++) {
            angle += gauss(rand) * 0.35;
            x += Math.cos(angle) * step;
            y += Math.sin(angle) * step;
            blob(g, x, y, size * (0.5 + rand()), colourFor(), alphaFor());
        }
    };

    const paintDeep = () => {
        const width = W + DEEP_PAD * 2;
        const height = Math.round(H * DEEP_SPAN);
        const unit = Math.max(width, height);
        const rand = seeded(1972);

        deep = deep || document.createElement("canvas");
        deep.width = width;
        deep.height = height;

        const g = deep.getContext("2d");
        g.clearRect(0, 0, width, height);

        // Red nebula: a few clouds, each a heap of soft glows with threads through it.
        const clouds = [
            { x: 0.84, y: 0.1, r: 0.36 },
            { x: 0.08, y: 0.36, r: 0.4 },
            { x: 0.68, y: 0.62, r: 0.3 },
            { x: 0.26, y: 0.9, r: 0.38 },
            { x: 0.98, y: 0.86, r: 0.24 }
        ];

        g.globalCompositeOperation = "lighter";

        for (const cloud of clouds) {
            const cx = cloud.x * width;
            const cy = cloud.y * height;
            const spread = cloud.r * unit;

            for (let i = 0; i < 26; i++) {
                blob(g,
                    cx + gauss(rand) * spread * 0.42,
                    cy + gauss(rand) * spread * 0.3,
                    spread * (0.22 + rand() * 0.5),
                    pick(rand, NEBULA_COLOURS),
                    0.03 + rand() * 0.07);
            }

            for (let i = 0; i < 5; i++) {
                filament(g, rand,
                    cx + gauss(rand) * spread * 0.3,
                    cy + gauss(rand) * spread * 0.2,
                    26, spread * 0.035, spread * 0.06,
                    () => pick(rand, NEBULA_COLOURS),
                    () => 0.04 + rand() * 0.07);
            }
        }

        // Dark dust lanes cut through the clouds.
        g.globalCompositeOperation = "source-over";

        for (const cloud of clouds) {
            const spread = cloud.r * unit;

            for (let i = 0; i < 3; i++) {
                filament(g, rand,
                    cloud.x * width + gauss(rand) * spread * 0.3,
                    cloud.y * height + gauss(rand) * spread * 0.2,
                    22, spread * 0.03, spread * 0.045,
                    () => [10, 3, 4],
                    () => 0.12 + rand() * 0.2);
            }
        }

        // The Milky Way: a diagonal band of glow and dust with a dark rift down the middle.
        const x0 = -0.1 * width;
        const y0 = 0.98 * height;
        const x1 = 1.1 * width;
        const y1 = 0.04 * height;
        const length = Math.hypot(x1 - x0, y1 - y0);
        const ux = (x1 - x0) / length;
        const uy = (y1 - y0) / length;
        const nx = -uy;
        const ny = ux;
        const band = Math.min(unit * 0.12, 220);
        const along = (t, off) => [x0 + ux * length * t + nx * off, y0 + uy * length * t + ny * off];

        g.globalCompositeOperation = "lighter";

        for (let i = 0; i < 80; i++) {
            const [x, y] = along(rand(), gauss(rand) * band * 0.32);
            blob(g, x, y, band * (0.55 + rand() * 0.8), rand() < 0.75 ? MILK : [255, 150, 128], 0.02 + rand() * 0.03);
        }

        const grains = Math.round(width * height * 0.0032);

        for (let i = 0; i < grains; i++) {
            const [x, y] = along(rand(), gauss(rand) * band * 0.42);
            const colour = pick(rand, STAR_COLOURS);
            const size = 0.45 + rand() * 0.7;
            g.fillStyle = rgba(colour, 0.14 + rand() * rand() * 0.7);
            g.fillRect(x, y, size, size);
        }

        g.globalCompositeOperation = "source-over";

        for (let i = 0; i < 46; i++) {
            const [x, y] = along(rand(), band * 0.06 + gauss(rand) * band * 0.12);
            blob(g, x, y, band * (0.07 + rand() * 0.18), [9, 3, 4], 0.18 + rand() * 0.3);
        }
    };

    // ---------- stars ----------

    const makeStars = () => {
        const rand = seeded(1947);

        layers = LAYERS.map((layer) => {
            const count = Math.round((W * H) / layer.area);
            const stars = [];

            for (let i = 0; i < count; i++) {
                const t = Math.pow(rand(), 2.4);
                stars.push({
                    x: rand(),
                    y: rand(),
                    r: layer.size[0] + (layer.size[1] - layer.size[0]) * t,
                    colour: pick(rand, STAR_COLOURS),
                    alpha: 0.55 + rand() * 0.45,
                    speed: 0.35 + rand() * 1.9,
                    phase: rand() * TAU,
                    amp: 0.08 + rand() * 0.42
                });
            }

            return { ...layer, stars };
        });

        // A handful of bright stars carry the faint cross a camera lens gives them.
        brights = [];
        const count = Math.max(4, Math.round((W * H) / 160000));

        for (let i = 0; i < count; i++) {
            brights.push({
                x: 0.04 + rand() * 0.92,
                y: 0.04 + rand() * 0.92,
                r: 2 + rand() * 1.4,
                colour: pick(rand, STAR_COLOURS.slice(0, 5)),
                spike: 10 + rand() * 14,
                speed: 0.25 + rand() * 0.5,
                phase: rand() * TAU
            });
        }
    };

    // ---------- meteors, showers, flares, stardust, satellite ----------

    const meteor = (x, y, angle, options = {}) => {
        const speed = options.speed || 700 + live() * 900;
        meteors.push({
            x,
            y,
            vx: Math.cos(angle) * speed,
            vy: Math.sin(angle) * speed,
            speed,
            length: options.length || 90 + live() * 220,
            life: options.life || 0.45 + live() * 0.7,
            width: options.width || 0.8 + live() * 1.1,
            colour: options.colour || pick(live, METEOR_COLOURS),
            age: 0
        });
    };

    // Mostly falling down and across, now left, now right.
    const fallingAngle = () => (live() < 0.5 ? 0.35 + live() * 0.6 : Math.PI - 0.35 - live() * 0.6);

    const randomMeteor = () => {
        meteor(-0.1 * W + live() * W * 1.2, -0.1 * H + live() * H * 0.62, fallingAngle());
    };

    const shower = (t) => {
        const rx = W * (0.15 + live() * 0.7);
        const ry = H * (live() * 0.25);
        const base = fallingAngle();
        const count = 5 + Math.floor(live() * 6);

        for (let i = 0; i < count; i++) {
            const offset = (live() - 0.5) * W * 0.3;
            queued.push({
                at: t + i * (0.08 + live() * 0.22),
                x: rx + Math.cos(base + Math.PI / 2) * offset,
                y: ry + Math.sin(base + Math.PI / 2) * offset,
                angle: base + (live() - 0.5) * 0.18
            });
        }
    };

    const launchSatellite = () => {
        const leftToRight = live() < 0.5;
        const y0 = H * (0.12 + live() * 0.45);
        satellite = {
            x0: leftToRight ? -40 : W + 40,
            y0,
            x1: leftToRight ? W + 40 : -40,
            y1: y0 + (live() - 0.5) * H * 0.4,
            duration: 22 + live() * 14,
            age: 0,
            altitude: 400 + Math.round(live() * 30)
        };
    };

    const interactive = "a, button, input, select, textarea, label, summary, iframe, [role='button'], [contenteditable], .rs-open";

    // ---------- drawing ----------

    const drawDeep = (scroll) => {
        if (!deep) {
            return;
        }

        const travel = Math.max(1, docH - H);
        const offset = Math.min(1, Math.max(0, scroll / travel)) * (deep.height - H);
        ctx.globalCompositeOperation = "source-over";
        ctx.globalAlpha = 1;
        ctx.drawImage(deep, -DEEP_PAD + pointer.x * 2, -offset + pointer.y * 2);
    };

    const drawStars = (t, scroll) => {
        ctx.globalCompositeOperation = "lighter";

        for (const layer of layers) {
            const shiftX = pointer.x * layer.pointer;
            const shiftY = pointer.y * layer.pointer - scroll * layer.scroll;

            for (const star of layer.stars) {
                const x = star.x * W + shiftX;
                let y = (star.y * H + shiftY) % H;

                if (y < 0) {
                    y += H;
                }

                const twinkle = reducedMotion ? 1 : 1 - star.amp * (0.5 + 0.5 * Math.sin(t * star.speed + star.phase));
                ctx.globalAlpha = star.alpha * twinkle;

                if (star.r < 0.75) {
                    // Tiny stars stay a full pixel and get fainter instead of smaller.
                    const size = Math.max(1, star.r * 2);
                    ctx.globalAlpha *= Math.min(1, 0.35 + star.r);
                    ctx.fillStyle = rgba(star.colour, 1);
                    ctx.fillRect(x - size / 2, y - size / 2, size, size);
                } else {
                    const size = star.r * 7;
                    ctx.drawImage(spriteFor(star.colour), x - size / 2, y - size / 2, size, size);
                }
            }
        }

        for (const star of brights) {
            const x = star.x * W + pointer.x * 16;
            let y = (star.y * H + pointer.y * 16 - scroll * 0.11) % H;

            if (y < 0) {
                y += H;
            }

            const glow = reducedMotion ? 1 : 0.86 + 0.14 * Math.sin(t * star.speed + star.phase);
            const size = star.r * 9;
            ctx.globalAlpha = glow;
            ctx.drawImage(spriteFor(star.colour), x - size / 2, y - size / 2, size, size);

            ctx.globalAlpha = 0.32 * glow;
            ctx.strokeStyle = rgba(star.colour, 1);
            ctx.lineWidth = 0.6;
            ctx.beginPath();
            ctx.moveTo(x - star.spike, y);
            ctx.lineTo(x + star.spike, y);
            ctx.moveTo(x, y - star.spike);
            ctx.lineTo(x, y + star.spike);
            ctx.stroke();
        }

        ctx.globalAlpha = 1;
    };

    const drawMeteors = (dt) => {
        ctx.globalCompositeOperation = "lighter";
        ctx.lineCap = "round";

        for (let i = meteors.length - 1; i >= 0; i--) {
            const m = meteors[i];
            m.age += dt;

            if (m.age >= m.life) {
                meteors.splice(i, 1);
                continue;
            }

            const progress = m.age / m.life;
            const alpha = Math.pow(Math.sin(Math.PI * progress), 0.6);
            const hx = m.x + m.vx * m.age;
            const hy = m.y + m.vy * m.age;
            const tail = m.length * Math.min(1, m.age / 0.16);
            const tx = hx - (m.vx / m.speed) * tail;
            const ty = hy - (m.vy / m.speed) * tail;
            const grad = ctx.createLinearGradient(hx, hy, tx, ty);
            grad.addColorStop(0, rgba(m.colour, alpha));
            grad.addColorStop(0.2, rgba(m.colour, alpha * 0.5));
            grad.addColorStop(1, rgba(m.colour, 0));

            ctx.globalAlpha = 1;
            ctx.strokeStyle = grad;
            ctx.lineWidth = m.width;
            ctx.beginPath();
            ctx.moveTo(hx, hy);
            ctx.lineTo(tx, ty);
            ctx.stroke();

            ctx.globalAlpha = alpha;
            ctx.drawImage(spriteFor(m.colour), hx - 7, hy - 7, 14, 14);
        }

        ctx.globalAlpha = 1;
    };

    const drawFlares = (dt) => {
        ctx.globalCompositeOperation = "lighter";

        for (let i = flares.length - 1; i >= 0; i--) {
            const f = flares[i];
            f.age += dt;
            const p = f.age / 1.2;

            if (p >= 1) {
                flares.splice(i, 1);
                continue;
            }

            const ease = 1 - Math.pow(1 - p, 3);
            ctx.globalAlpha = Math.pow(1 - p, 2);
            ctx.drawImage(spriteFor([255, 236, 200]), f.x - 60 * (1 - p * 0.5), f.y - 60 * (1 - p * 0.5), 120 * (1 - p * 0.5), 120 * (1 - p * 0.5));

            ctx.lineWidth = 1;
            ctx.strokeStyle = rgba([255, 226, 180], 0.7 * Math.pow(1 - p, 2));
            ctx.beginPath();
            ctx.arc(f.x, f.y, 12 + 150 * ease, 0, TAU);
            ctx.stroke();

            if (p > 0.12) {
                const q = (p - 0.12) / 0.88;
                ctx.strokeStyle = rgba(BLUE, 0.45 * Math.pow(1 - q, 2));
                ctx.beginPath();
                ctx.arc(f.x, f.y, 8 + 90 * (1 - Math.pow(1 - q, 3)), 0, TAU);
                ctx.stroke();
            }
        }

        ctx.globalAlpha = 1;
    };

    const drawDust = (dt) => {
        ctx.globalCompositeOperation = "lighter";
        const drag = Math.exp(-2.2 * dt);

        for (let i = dust.length - 1; i >= 0; i--) {
            const d = dust[i];
            d.age += dt;

            if (d.age >= d.life) {
                dust.splice(i, 1);
                continue;
            }

            d.vx *= drag;
            d.vy = d.vy * drag + 14 * dt;
            d.x += d.vx * dt;
            d.y += d.vy * dt;

            const fade = Math.pow(1 - d.age / d.life, 1.4);
            const flicker = 0.7 + 0.3 * Math.sin(d.age * 26 + d.phase);
            const size = d.r * 7;
            ctx.globalAlpha = fade * flicker;
            ctx.drawImage(spriteFor(d.colour), d.x - size / 2, d.y - size / 2, size, size);
        }

        ctx.globalAlpha = 1;
    };

    const drawSatellite = (dt) => {
        if (!satellite) {
            return;
        }

        satellite.age += dt;
        const p = satellite.age / satellite.duration;

        if (p >= 1) {
            satellite = null;
            return;
        }

        const x = satellite.x0 + (satellite.x1 - satellite.x0) * p;
        const y = satellite.y0 + (satellite.y1 - satellite.y0) * p;
        const fade = Math.min(1, p * 10, (1 - p) * 10);

        ctx.globalCompositeOperation = "lighter";
        ctx.globalAlpha = fade;
        ctx.drawImage(spriteFor(CREAM), x - 7, y - 7, 14, 14);

        // Ground Control's tracking brackets and tag.
        ctx.globalCompositeOperation = "source-over";
        ctx.globalAlpha = fade * 0.85;
        ctx.strokeStyle = rgba(GOLD, 1);
        ctx.lineWidth = 1;
        const b = 11;
        const c = 4;
        ctx.beginPath();
        ctx.moveTo(x - b, y - b + c);
        ctx.lineTo(x - b, y - b);
        ctx.lineTo(x - b + c, y - b);
        ctx.moveTo(x + b - c, y - b);
        ctx.lineTo(x + b, y - b);
        ctx.lineTo(x + b, y - b + c);
        ctx.moveTo(x + b, y + b - c);
        ctx.lineTo(x + b, y + b);
        ctx.lineTo(x + b - c, y + b);
        ctx.moveTo(x - b + c, y + b);
        ctx.lineTo(x - b, y + b);
        ctx.lineTo(x - b, y + b - c);
        ctx.moveTo(x + b, y - b);
        ctx.lineTo(x + b + 14, y - b - 14);
        ctx.lineTo(x + b + 30, y - b - 14);
        ctx.stroke();

        ctx.fillStyle = rgba(GOLD, 1);
        ctx.font = "700 10px 'Space Mono', ui-monospace, monospace";
        ctx.fillText("TIN CAN · MAJOR TOM", x + b + 34, y - b - 11);
        ctx.fillStyle = rgba(BLUE, 1);
        ctx.font = "400 10px 'Space Mono', ui-monospace, monospace";
        ctx.fillText(`ALT ${satellite.altitude} KM · ${(7.66 + Math.sin(satellite.age) * 0.01).toFixed(2)} KM/S`, x + b + 34, y - b + 3);
        ctx.globalAlpha = 1;
    };

    // ---------- loop ----------

    let last = 0;
    let running = false;

    const frame = (now) => {
        const t = now / 1000;
        const dt = Math.min(0.05, Math.max(0, t - last));
        last = t;

        pointer.x += (pointer.tx - pointer.x) * 0.05;
        pointer.y += (pointer.ty - pointer.y) * 0.05;

        const scroll = window.scrollY;
        ctx.setTransform(dpr, 0, 0, dpr, 0, 0);
        ctx.clearRect(0, 0, W, H);

        drawDeep(scroll);
        drawStars(t, scroll);

        if (!reducedMotion) {
            if (t >= nextMeteor) {
                randomMeteor();
                nextMeteor = t + 0.5 + live() * 2.6;
            }

            if (t >= nextShower) {
                shower(t);
                nextShower = t + 14 + live() * 16;
            }

            if (!satellite && t >= nextSatellite) {
                launchSatellite();
                nextSatellite = t + 40 + live() * 30;
            }

            for (let i = queued.length - 1; i >= 0; i--) {
                if (t >= queued[i].at) {
                    meteor(queued[i].x, queued[i].y, queued[i].angle);
                    queued.splice(i, 1);
                }
            }

            drawSatellite(dt);
            drawMeteors(dt);
            drawFlares(dt);
            drawDust(dt);
        }

        ctx.globalCompositeOperation = "source-over";

        if (running) {
            requestAnimationFrame(frame);
        }
    };

    const resize = (force) => {
        const width = canvas.clientWidth;
        const height = canvas.clientHeight;

        if (!width || !height) {
            return;
        }

        const regrow = force || !deep || Math.abs(width - W) > 2 || Math.abs(height - H) / Math.max(1, H) > 0.2;
        W = width;
        H = height;
        dpr = Math.min(2, window.devicePixelRatio || 1);
        canvas.width = Math.round(W * dpr);
        canvas.height = Math.round(H * dpr);
        docH = root.scrollHeight;

        if (regrow) {
            paintDeep();
            makeStars();
        }

        if (!running) {
            frame(performance.now());
        }
    };

    let resizeTimer = 0;
    window.addEventListener("resize", () => {
        window.clearTimeout(resizeTimer);
        resizeTimer = window.setTimeout(() => resize(false), 160);
    });

    if ("ResizeObserver" in window) {
        new ResizeObserver(() => {
            docH = root.scrollHeight;
        }).observe(document.body);
    }

    root.classList.add("rs-sky-on");
    resize(true);

    if (reducedMotion) {
        return;
    }

    running = true;
    requestAnimationFrame((now) => {
        last = now / 1000;
        requestAnimationFrame(frame);
    });

    if (finePointer) {
        let lastDust = 0;

        window.addEventListener("pointermove", (event) => {
            pointer.tx = (event.clientX / W - 0.5) * 2;
            pointer.ty = (event.clientY / H - 0.5) * 2;

            const now = performance.now();

            if (now - lastDust < 18 || !(event.target instanceof Element) || !event.target.closest("[data-rs-stardust]")) {
                return;
            }

            lastDust = now;

            for (let i = 0; i < 3; i++) {
                dust.push({
                    x: event.clientX + (live() - 0.5) * 8,
                    y: event.clientY + (live() - 0.5) * 8,
                    vx: (live() - 0.5) * 46 + (event.movementX || 0) * 3,
                    vy: (live() - 0.5) * 46 - 10,
                    r: 0.5 + live() * 1.5,
                    life: 0.8 + live() * 1.2,
                    colour: pick(live, DUST_COLOURS),
                    phase: live() * TAU,
                    age: 0
                });
            }

            if (dust.length > 240) {
                dust.splice(0, dust.length - 240);
            }
        }, { passive: true });
    }

    // Click the sky (anywhere that is not a control) and it flares, throwing off a few falling stars.
    window.addEventListener("pointerdown", (event) => {
        if (event.button !== 0 || !(event.target instanceof Element) || event.target.closest(interactive)) {
            return;
        }

        flares.push({ x: event.clientX, y: event.clientY, age: 0 });

        for (let i = 0; i < 4; i++) {
            meteor(event.clientX, event.clientY, live() * TAU, { speed: 500 + live() * 500, length: 50 + live() * 90, life: 0.35 + live() * 0.35 });
        }
    }, { passive: true });

    // ---------- warp: the starfield behind the countdown ----------

    const warpCanvas = document.querySelector("[data-rs-warp]");
    const preloader = warpCanvas && warpCanvas.closest("[data-rs-preloader]");

    if (warpCanvas && preloader && !root.classList.contains("rs-seen")) {
        const w = warpCanvas.getContext("2d");
        const rand = seeded(1969);
        const stars = Array.from({ length: 260 }, () => ({
            x: (rand() - 0.5) * 2,
            y: (rand() - 0.5) * 2,
            z: rand(),
            colour: pick(rand, STAR_COLOURS)
        }));
        const started = performance.now();
        let pw = 0;
        let ph = 0;

        const warp = (now) => {
            if (preloader.hidden) {
                return;
            }

            const width = warpCanvas.clientWidth;
            const height = warpCanvas.clientHeight;

            if (width !== pw || height !== ph) {
                pw = width;
                ph = height;
                warpCanvas.width = Math.round(width * dpr);
                warpCanvas.height = Math.round(height * dpr);
            }

            const elapsed = (now - started) / 1000;
            const lift = preloader.classList.contains("is-lift") || preloader.classList.contains("is-done");
            const speed = lift ? 3.2 : 0.08 + Math.min(1, elapsed / 1.1) * 0.9;
            const cx = width / 2;
            const cy = height / 2;
            const scale = Math.max(width, height) * 0.5;

            w.setTransform(dpr, 0, 0, dpr, 0, 0);
            w.clearRect(0, 0, width, height);
            w.globalCompositeOperation = "lighter";
            w.lineCap = "round";

            for (const s of stars) {
                const pz = s.z;
                s.z -= speed * 0.016;

                if (s.z <= 0.02) {
                    s.x = (rand() - 0.5) * 2;
                    s.y = (rand() - 0.5) * 2;
                    s.z = 1;
                    continue;
                }

                const x = cx + (s.x / s.z) * scale * 0.5;
                const y = cy + (s.y / s.z) * scale * 0.5;
                const px = cx + (s.x / pz) * scale * 0.5;
                const py = cy + (s.y / pz) * scale * 0.5;
                const near = 1 - s.z;

                w.strokeStyle = rgba(s.colour, Math.min(1, near * 1.4));
                w.lineWidth = 0.4 + near * 2;
                w.beginPath();
                w.moveTo(px, py);
                w.lineTo(x, y);
                w.stroke();
            }

            requestAnimationFrame(warp);
        };

        requestAnimationFrame(warp);
    }
})();
