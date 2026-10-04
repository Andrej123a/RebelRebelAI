// Cut-up: everything you can press on the public pages is a scrap of paper.
// - every scrap gets its own jagged cut, and is re-cut a few times a second while
//   you hover or focus it (it boils),
// - button labels shuffle their letters when the mouse comes over them,
// - buttons lean towards a mouse pointer,
// - pressing anything, the cast's calls included, throws a handful of stars.
// The scraps keep the cut in the CSS without the script. With reduced motion they
// are cut once and then left alone.
(() => {
    const reducedMotion = window.matchMedia("(prefers-reduced-motion: reduce)").matches;
    const finePointer = window.matchMedia("(hover: hover) and (pointer: fine)").matches;

    const SCRAPS = [
        ".rs-chip",
        ".rs-open-close",
        ".rr-beer-filter",
        ".rr-guide-chat-prompts button",
        ".rr-guide-recovery-actions button",
        ".rr-guide-feedback button",
        ".rr-guide-compare-button",
    ].join(", ");

    const PRESSABLE = SCRAPS + ", .rs-ticket, .rs-ego, .rs-ego-act, .rs-cast-link, .rs-open-toggle";
    const SCRAMBLED = ".rs-ticket-main";
    const MAGNETIC = ".rs-ticket";

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

    // ---------- wiring ----------

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
