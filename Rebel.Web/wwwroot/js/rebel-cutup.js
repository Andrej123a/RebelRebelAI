// Cut-up: everything you can press on the public pages is a scrap of paper.
// - every scrap gets its own jagged cut, and is re-cut a few times a second while
//   you hover or focus it (it boils),
// - button labels shuffle their letters when the mouse comes over them,
// - buttons lean towards a mouse pointer,
// - pressing anything throws a handful of stars,
// - the header nav is a ransom note: each letter cut out of a different newspaper.
// The scraps keep the cut in the CSS without the script. With reduced motion they
// are cut once and then left alone, and the ransom note stays still.
(() => {
    const reducedMotion = window.matchMedia("(prefers-reduced-motion: reduce)").matches;
    const finePointer = window.matchMedia("(hover: hover) and (pointer: fine)").matches;

    const SCRAPS = [
        ".rs-btn",
        ".rs-chip",
        ".rs-menu-tab",
        ".rs-cargo-action",
        ".rs-food-pair",
        ".rs-open-close",
        ".rr-beer-filter",
        ".rr-beer-outro-actions a",
        ".rr-beer-empty button",
        ".rr-beer-action",
        ".rr-beer-callout a",
        ".rr-guide-chat-form button",
        ".rr-guide-submit",
        ".rr-guide-chat-prompts button",
        ".rr-guide-recovery-actions button",
        ".rr-guide-feedback button",
        ".rr-guide-compare-button",
    ].join(", ");

    const PRESSABLE = SCRAPS + ", .rs-ticket, .rs-nav-link, .rs-open-toggle";
    const SCRAMBLED = ".rs-btn, .rs-ticket-main, .rr-beer-action, .rr-guide-chat-form button, .rr-guide-submit";
    const MAGNETIC = ".rs-btn, .rs-ticket, .rr-beer-action, .rr-guide-chat-form button, .rr-guide-submit";

    const random = (min, max) => min + Math.random() * (max - min);
    const pick = (list) => list[Math.floor(Math.random() * list.length)];
    const point = (x, y) => `${x.toFixed(1)}% ${y.toFixed(1)}%`;

    // ---------- cuts ----------

    // A scissor cut around the label: a few notches along each long edge, a kink in
    // each short one, never deep enough to touch the letters.
    const cut = () => {
        const points = [];
        const top = 2 + Math.floor(Math.random() * 3);
        const bottom = 2 + Math.floor(Math.random() * 3);

        points.push(point(random(0, 3), random(0, 11)));

        for (let i = 1; i <= top; i++) {
            points.push(point((i / (top + 1)) * 100 + random(-7, 7), random(0, 10)));
        }

        points.push(point(random(97, 100), random(0, 11)));
        points.push(point(random(95, 100), random(35, 65)));
        points.push(point(random(97, 100), random(89, 100)));

        for (let i = bottom; i >= 1; i--) {
            points.push(point((i / (bottom + 1)) * 100 + random(-7, 7), random(90, 100)));
        }

        points.push(point(random(0, 3), random(89, 100)));
        points.push(point(random(0, 5), random(35, 65)));

        return `polygon(${points.join(", ")})`;
    };

    const recut = (scrap) => scrap.style.setProperty("--cut", cut());

    const boil = (scrap) => {
        if (reducedMotion) {
            return;
        }

        let timer = 0;

        const start = () => {
            if (!timer) {
                recut(scrap);
                timer = window.setInterval(() => recut(scrap), 140);
            }
        };

        const stop = () => {
            window.clearInterval(timer);
            timer = 0;
        };

        scrap.addEventListener("pointerenter", start);
        scrap.addEventListener("focus", start);
        scrap.addEventListener("pointerleave", () => {
            if (document.activeElement !== scrap) {
                stop();
            }
        });
        scrap.addEventListener("blur", stop);
    };

    const scraps = new WeakSet();

    const cutAll = (scope) => {
        scope.querySelectorAll(SCRAPS).forEach((scrap) => {
            if (scraps.has(scrap)) {
                return;
            }

            scraps.add(scrap);
            recut(scrap);
            boil(scrap);
        });
    };

    // ---------- letter shuffle ----------

    const GLYPHS = "ABCDEFGHIJKLMNOPQRSTUVWXYZ#&*!?%/+";

    const labelTexts = (element) => {
        const walker = document.createTreeWalker(element, NodeFilter.SHOW_TEXT, {
            acceptNode: (node) =>
                node.nodeValue.trim().length > 1 && !node.parentElement.closest("i, svg, [aria-hidden='true'], .visually-hidden")
                    ? NodeFilter.FILTER_ACCEPT
                    : NodeFilter.FILTER_REJECT,
        });
        const nodes = [];

        while (walker.nextNode()) {
            nodes.push(walker.currentNode);
        }

        return nodes;
    };

    const scramble = (element) => {
        if (element.dataset.rsScrambling) {
            return;
        }

        const nodes = labelTexts(element);

        if (nodes.length === 0) {
            return;
        }

        const originals = nodes.map((node) => node.nodeValue);
        const frames = 10;
        let frame = 0;

        element.dataset.rsScrambling = "true";
        element.style.width = `${element.offsetWidth}px`;

        const tick = () => {
            frame++;

            nodes.forEach((node, index) => {
                const text = originals[index];
                const settled = Math.floor((frame / frames) * text.length);

                node.nodeValue = Array.from(text, (char, at) =>
                    at < settled || char.trim() === "" ? char : pick(GLYPHS)).join("");
            });

            if (frame < frames) {
                window.setTimeout(tick, 38);
                return;
            }

            nodes.forEach((node, index) => { node.nodeValue = originals[index]; });
            element.style.width = "";
            delete element.dataset.rsScrambling;
        };

        tick();
    };

    // ---------- magnetic pull ----------

    const magnetise = (element) => {
        element.addEventListener("pointermove", (event) => {
            const box = element.getBoundingClientRect();
            const x = (event.clientX - (box.left + box.width / 2)) * 0.16;
            const y = (event.clientY - (box.top + box.height / 2)) * 0.3;
            const clamp = (value) => Math.max(-8, Math.min(8, value));

            element.style.translate = `${clamp(x).toFixed(1)}px ${clamp(y).toFixed(1)}px`;
        });

        element.addEventListener("pointerleave", () => { element.style.translate = ""; });
    };

    // ---------- sparks ----------

    const SPARK_COLOURS = ["#f2c23a", "#ffd86a", "#e2401a", "#9ad3f0", "#fff1dc"];
    let sparkLayer = null;

    const sparks = (x, y) => {
        if (!sparkLayer) {
            sparkLayer = document.createElement("div");
            sparkLayer.className = "rs-sparks";
            sparkLayer.setAttribute("aria-hidden", "true");
            document.body.append(sparkLayer);
        }

        for (let i = 0; i < 8; i++) {
            const spark = document.createElementNS("http://www.w3.org/2000/svg", "svg");
            const use = document.createElementNS("http://www.w3.org/2000/svg", "use");
            const angle = random(0, Math.PI * 2);
            const distance = random(28, 74);
            const size = random(9, 20);

            use.setAttribute("href", "#rs-star");
            spark.setAttribute("viewBox", "0 0 100 100");
            spark.append(use);
            spark.style.left = `${x - size / 2}px`;
            spark.style.top = `${y - size / 2}px`;
            spark.style.width = `${size}px`;
            spark.style.height = `${size}px`;
            spark.style.fill = pick(SPARK_COLOURS);
            sparkLayer.append(spark);

            spark.animate(
                [
                    { transform: "translate(0, 0) rotate(0deg) scale(0.4)", opacity: 1 },
                    { transform: `translate(${Math.cos(angle) * distance}px, ${Math.sin(angle) * distance}px) rotate(${random(-200, 200)}deg) scale(1)`, opacity: 1, offset: 0.55 },
                    { transform: `translate(${Math.cos(angle) * distance * 1.25}px, ${Math.sin(angle) * distance * 1.25 + 18}px) rotate(${random(-260, 260)}deg) scale(0)`, opacity: 0 },
                ],
                { duration: random(520, 760), easing: "cubic-bezier(0.2, 0.7, 0.2, 1)" })
                .finished.then(() => spark.remove(), () => spark.remove());
        }
    };

    // ---------- the ransom note ----------

    // Newsprint only: cream, bone, grey and the odd black headline.
    const PAPERS = [
        ["var(--z-cream)", "var(--z-ink)"],
        ["var(--z-news)", "var(--z-ink)"],
        ["#e4ddd0", "var(--z-ink)"],
        ["#cfc8bb", "var(--z-ink)"],
        ["var(--z-ink)", "var(--z-cream)"],
    ];

    const FACES = [
        { font: "var(--rs-stencil)", size: [17, 21] },
        { font: "var(--rs-display)", size: [17, 21] },
        { font: "var(--rs-news)", size: [16, 19], style: "italic", lower: true },
        { font: "var(--rs-mono)", size: [13, 15] },
    ];

    const letterCut = () =>
        `polygon(${point(random(0, 12), random(0, 12))}, ${point(random(88, 100), random(0, 12))}, ` +
        `${point(random(88, 100), random(88, 100))}, ${point(random(0, 12), random(88, 100))})`;

    const shuffleNote = (note) => {
        let lastPaper = null;

        note.querySelectorAll("span:not(.rs-ransom-gap)").forEach((letter) => {
            let paper = pick(PAPERS);

            while (paper === lastPaper) {
                paper = pick(PAPERS);
            }

            lastPaper = paper;

            const face = pick(FACES);
            const style = letter.style;

            style.setProperty("--l-bg", paper[0]);
            style.setProperty("--l-ink", paper[1]);
            style.setProperty("--l-font", face.font);
            style.setProperty("--l-size", `${Math.round(random(face.size[0], face.size[1]))}px`);
            style.setProperty("--l-style", face.style ?? "normal");
            // An italic lower-case l or i reads as a slash; those stay capitals.
            const lower = face.lower && !/[il]/i.test(letter.textContent) && Math.random() < 0.6;

            style.setProperty("--l-case", lower ? "lowercase" : "uppercase");
            style.setProperty("--l-turn", `${random(-9, 9).toFixed(1)}deg`);
            style.setProperty("--l-lift", `${random(-2.5, 2.5).toFixed(1)}px`);
            style.setProperty("--l-cut", letterCut());
        });
    };

    const ransom = (nav) => {
        nav.querySelectorAll("[data-rs-ransom]").forEach((link) => {
            const text = link.textContent.trim();
            const label = document.createElement("span");
            const note = document.createElement("span");

            label.className = "visually-hidden";
            label.textContent = text;
            note.className = "rs-ransom";
            note.setAttribute("aria-hidden", "true");

            for (const char of text) {
                const letter = document.createElement("span");

                if (char === " ") {
                    letter.className = "rs-ransom-gap";
                } else {
                    letter.textContent = char;
                }

                note.append(letter);
            }

            link.replaceChildren(label, note);
            shuffleNote(note);

            if (reducedMotion) {
                return;
            }

            // Hold the link's place while its letters are swapped, so the pointer
            // never loses it.
            const reshuffle = () => {
                link.style.width = `${link.offsetWidth}px`;
                [0, 70, 140].forEach((delay) => window.setTimeout(() => shuffleNote(note), delay));
            };

            link.addEventListener("pointerenter", reshuffle);
            link.addEventListener("focus", reshuffle);
            link.addEventListener("pointerleave", () => { link.style.width = ""; });
            link.addEventListener("blur", () => { link.style.width = ""; });
        });

        nav.classList.add("is-ransom");
    };

    // ---------- wiring ----------

    document.querySelectorAll(".rs-nav").forEach(ransom);
    cutAll(document);

    // Scraps that arrive later (Rebel AI's answers, the beer menu's filters) get cut too.
    if ("MutationObserver" in window) {
        let queued = false;

        const isNew = (record) =>
            record.target !== sparkLayer &&
            Array.from(record.addedNodes).some((node) => node.nodeType === Node.ELEMENT_NODE);

        new MutationObserver((records) => {
            if (!queued && records.some(isNew)) {
                queued = true;
                requestAnimationFrame(() => {
                    queued = false;
                    cutAll(document);
                });
            }
        }).observe(document.body, { childList: true, subtree: true });
    }

    if (reducedMotion) {
        return;
    }

    document.addEventListener("pointerover", (event) => {
        if (event.pointerType !== "mouse") {
            return;
        }

        const target = event.target.closest(SCRAMBLED);

        if (target && !target.contains(event.relatedTarget)) {
            scramble(target);
        }
    });

    if (finePointer) {
        const magnets = new WeakSet();

        document.addEventListener("pointerover", (event) => {
            const target = event.target.closest(MAGNETIC);

            if (target && !magnets.has(target)) {
                magnets.add(target);
                magnetise(target);
            }
        });
    }

    document.addEventListener("pointerdown", (event) => {
        if (event.button === 0 && event.target.closest(PRESSABLE)) {
            sparks(event.clientX, event.clientY);
        }
    });

    // Enter or Space on a focused button sparks from its middle.
    document.addEventListener("click", (event) => {
        const target = event.detail === 0 && event.target.closest(PRESSABLE);

        if (target) {
            const box = target.getBoundingClientRect();
            sparks(box.left + box.width / 2, box.top + box.height / 2);
        }
    });
})();
