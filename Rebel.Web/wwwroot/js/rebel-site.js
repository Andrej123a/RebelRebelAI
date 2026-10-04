// Shared motion for the public pages:
// - the full-screen #open menu,
// - blocks marked data-rs-reveal come into focus as they scroll in,
// - blocks marked data-rs-drift move at their own speed while scrolling,
// - headlines rise word by word as they scroll in,
// - Bowie's looks (Art/Looks) move only while on screen,
// - on the home stage the menus Life on Mars? and Ziggy hold up flip their photos over,
//   and Aladdin Sane walks you through the pub,
// - the home page's "How Rebel works" stage, one character at a time while you scroll.
// The night sky, falling stars and stardust live in rebel-sky.js.
// Without JS or with reduced motion everything is simply shown.
(() => {
    const root = document.documentElement;
    const reducedMotion = window.matchMedia("(prefers-reduced-motion: reduce)").matches;

    // ---------- #open menu ----------

    const menu = document.querySelector("[data-rs-open]");
    const toggle = document.querySelector("[data-rs-open-toggle]");

    if (menu && toggle) {
        const closeLink = menu.querySelector("[data-rs-open-close]");
        const links = Array.from(menu.querySelectorAll("[data-rs-preview]"));
        const previews = Array.from(menu.querySelectorAll("[data-rs-preview-img]"));
        const focusable = () => Array.from(menu.querySelectorAll("a[href], button:not([disabled])"));
        let lastFocus = null;

        const preview = (key) => {
            links.forEach((link) => link.classList.toggle("is-previewed", link.dataset.rsPreview === key));
            previews.forEach((img) => img.classList.toggle("is-shown", img.dataset.rsPreviewImg === key));
        };

        const current = links.find((link) => link.getAttribute("aria-current") === "page") ?? links[0];

        const open = () => {
            lastFocus = document.activeElement;
            menu.classList.add("is-open");
            root.classList.add("rs-open-lock");
            toggle.setAttribute("aria-expanded", "true");

            if (current) {
                preview(current.dataset.rsPreview);
            }

            window.setTimeout(() => (current ?? closeLink)?.focus(), 60);
        };

        const close = () => {
            menu.classList.remove("is-open");
            root.classList.remove("rs-open-lock");
            toggle.setAttribute("aria-expanded", "false");

            if (location.hash === "#open") {
                history.replaceState(null, "", location.pathname + location.search);
            }

            (lastFocus instanceof HTMLElement ? lastFocus : toggle).focus();
        };

        toggle.addEventListener("click", (event) => {
            event.preventDefault();
            open();
        });

        closeLink?.addEventListener("click", (event) => {
            event.preventDefault();
            close();
        });

        menu.addEventListener("keydown", (event) => {
            if (event.key === "Escape") {
                event.preventDefault();
                close();
                return;
            }

            if (event.key !== "Tab") {
                return;
            }

            const items = focusable();
            const first = items[0];
            const last = items[items.length - 1];

            if (event.shiftKey && document.activeElement === first) {
                event.preventDefault();
                last.focus();
            } else if (!event.shiftKey && document.activeElement === last) {
                event.preventDefault();
                first.focus();
            }
        });

        links.forEach((link) => {
            link.addEventListener("pointerenter", () => preview(link.dataset.rsPreview));
            link.addEventListener("focus", () => preview(link.dataset.rsPreview));
        });

        if (location.hash === "#open") {
            open();
        }
    }

    // ---------- header: when it scrolls away, Open floats in the corner ----------

    const header = document.querySelector(".rs-header");

    if (header && "IntersectionObserver" in window) {
        new IntersectionObserver(
            ([entry]) => root.classList.toggle("rs-header-gone", !entry.isIntersecting),
            { threshold: 0 }).observe(header);
    }

    // ---------- reveal: out of focus, then sharp ----------

    const targets = document.querySelectorAll("[data-rs-reveal]");

    if (reducedMotion || !("IntersectionObserver" in window)) {
        targets.forEach((target) => target.classList.add("is-in-view"));
    } else {
        const observer = new IntersectionObserver(
            (entries) => {
                entries.forEach((entry) => {
                    if (entry.isIntersecting) {
                        entry.target.classList.add("is-in-view");
                        observer.unobserve(entry.target);
                    }
                });
            },
            { threshold: 0.12 });

        targets.forEach((target) => observer.observe(target));
    }

    if (reducedMotion) {
        return;
    }

    // ---------- headlines rise word by word out of their own line ----------

    const risers = Array.from(document.querySelectorAll("main h1, main h2, main .rs-changes-job, .rs-footer-word"))
        .filter((heading) => heading.textContent.trim() && !heading.closest("form, [data-rs-plain]"));

    const splitWords = (node, count) => {
        Array.from(node.childNodes).forEach((child) => {
            if (child.nodeType === Node.TEXT_NODE) {
                const fragment = document.createDocumentFragment();

                child.textContent.split(/(\s+)/).forEach((part) => {
                    if (!part) {
                        return;
                    }

                    if (/^\s+$/.test(part)) {
                        fragment.append(part);
                        return;
                    }

                    const word = document.createElement("span");
                    const ink = document.createElement("span");
                    word.className = "rs-w";
                    ink.textContent = part;
                    ink.style.setProperty("--w", count.next++);
                    word.append(ink);
                    fragment.append(word);
                });

                child.replaceWith(fragment);
            } else if (child.nodeType === Node.ELEMENT_NODE && !child.matches("br, svg, img, input")) {
                splitWords(child, count);
            }
        });
    };

    if (risers.length && "IntersectionObserver" in window) {
        const riseObserver = new IntersectionObserver(
            (entries) => {
                entries.forEach((entry) => {
                    if (entry.isIntersecting) {
                        entry.target.classList.add("is-risen");
                        riseObserver.unobserve(entry.target);
                    }
                });
            },
            { threshold: 0.2 });

        risers.forEach((heading) => {
            splitWords(heading, { next: 0 });
            heading.classList.add("rs-rise");
            riseObserver.observe(heading);
        });
    }

    // ---------- drift: parallax by data-rs-drift="0.2" ----------

    const drifters = Array.from(document.querySelectorAll("[data-rs-drift]"))
        .map((element) => ({ element, factor: parseFloat(element.dataset.rsDrift) || 0 }));

    if (drifters.length) {
        let queued = false;

        const update = () => {
            queued = false;
            const middle = window.innerHeight / 2;

            drifters.forEach(({ element, factor }) => {
                const box = element.getBoundingClientRect();

                if (box.bottom < -200 || box.top > window.innerHeight + 200) {
                    return;
                }

                const offset = (box.top + box.height / 2 - middle) * factor;
                element.style.setProperty("--rs-drift", `${offset.toFixed(1)}px`);
            });
        };

        const queue = () => {
            if (!queued) {
                queued = true;
                requestAnimationFrame(update);
            }
        };

        window.addEventListener("scroll", queue, { passive: true });
        window.addEventListener("resize", queue);
        update();
    }

    // ---------- the menus held up on the stage flip their photos over the top ----------

    document.querySelectorAll(".rs-carrier-photos").forEach((photos, index) => {
        const shots = Array.from(photos.querySelectorAll("img"));
        const carrier = photos.closest("[data-rs-look]");

        if (shots.length < 2 || !carrier) {
            return;
        }

        let shown = 0;
        let busy = false;
        let timer = 0;

        photos.classList.add("is-flipbook");
        shots[0].classList.add("is-shown");

        const flip = () => {
            if (busy) {
                return;
            }

            busy = true;
            const leaving = shots[shown];
            shown = (shown + 1) % shots.length;
            shots[shown].classList.add("is-shown");
            leaving.classList.add("is-leaving");

            window.setTimeout(() => {
                leaving.classList.remove("is-shown", "is-leaving");
                busy = false;
            }, 720);
        };

        carrier.addEventListener("pointerenter", flip);
        carrier.addEventListener("focus", flip);

        // While he is on screen (on the stage: while it is his turn), a new photo every
        // few seconds, each on his own beat.
        if ("IntersectionObserver" in window) {
            new IntersectionObserver(([entry]) => {
                window.clearInterval(timer);

                if (entry.isIntersecting) {
                    timer = window.setInterval(() => {
                        if (!carrier.closest(".is-staged") || carrier.classList.contains("is-current")) {
                            flip();
                        }
                    }, 5200 + index * 900);
                }
            }).observe(photos);
        }
    });

    // ---------- Bowie's looks: they move only while on screen ----------

    const looks = Array.from(document.querySelectorAll(".rs-look"));
    const onScreen = new Set();
    let refreshLooks = () => {};

    if (looks.length && "IntersectionObserver" in window) {
        // On the Changes stage only the look in the spotlight is live.
        const refresh = (look) => {
            const slot = look.closest("[data-rs-look]");
            const staged = Boolean(slot && slot.closest(".is-staged"));
            look.classList.toggle("is-live", onScreen.has(look) && (!staged || slot.classList.contains("is-current")));
        };

        const watcher = new IntersectionObserver(
            (entries) => {
                entries.forEach((entry) => {
                    if (entry.isIntersecting) {
                        onScreen.add(entry.target);
                    } else {
                        onScreen.delete(entry.target);
                    }

                    refresh(entry.target);
                });
            },
            { rootMargin: "40px" });

        looks.forEach((look) => watcher.observe(look));
        refreshLooks = () => looks.forEach(refresh);
    }

    // ---------- Ch-ch-changes: the cast takes the stage one look at a time ----------

    const changes = document.querySelector("[data-rs-changes]");

    if (changes) {
        const slots = Array.from(changes.querySelectorAll("[data-rs-look]"));
        const stage = changes.querySelector(".rs-changes-stage");
        let current = -1;
        let queued = false;

        // Each look has a stretch of the scroll; Aladdin Sane's tour has one per stop.
        const weights = slots.map((slot) => Math.max(1, Number(slot.dataset.rsStops) || 1));
        const total = weights.reduce((sum, weight) => sum + weight, 0);
        const starts = weights.map((_, i) => weights.slice(0, i).reduce((sum, weight) => sum + weight, 0));

        // Aladdin Sane walks you through the pub: the wall of photos slides past while you
        // scroll, he walks while it moves, and at each stop he says what it is.
        const tours = new Map();

        slots.forEach((slot) => {
            const wall = slot.querySelector("[data-rs-tour]");

            if (!wall) {
                return;
            }

            const stops = Array.from(wall.querySelectorAll("[data-rs-tour-stop]"));
            const line = slot.querySelector(".rs-changes-says .rs-ego-line");
            const bubble = line?.closest(".rs-ego-bubble");
            const steps = Array.from(slot.querySelectorAll(".rs-tour-steps i"));
            const ease = (x) => (x < 0.5 ? 2 * x * x : 1 - Math.pow(-2 * x + 2, 2) / 2);
            let here = -1;
            let last = null;
            let resting = 0;

            const centre = (i) => stops[i].offsetLeft + stops[i].offsetWidth / 2;

            tours.set(slot, (along) => {
                const span = Math.min(stops.length - 0.0001, Math.max(0, along) * stops.length);
                const stop = Math.floor(span);
                // The first part of each stretch is the walk over from the last stop.
                const walked = stop > 0 ? ease(Math.min(1, (span - stop) / 0.4)) : 1;
                const at = stop - 1 + walked;
                const from = Math.max(0, Math.floor(at));
                const to = Math.min(stops.length - 1, from + 1);
                const x = centre(from) + (centre(to) - centre(from)) * (at - from);
                const focus = parseFloat(getComputedStyle(wall).getPropertyValue("--tour-focus")) || 0.5;

                wall.style.transform = `translateX(${(wall.parentElement.clientWidth * focus - x).toFixed(1)}px)`;

                // He walks only while the wall is moving.
                if (last !== null && Math.abs(at - last) > 0.002) {
                    slot.classList.add("is-walking");
                    window.clearTimeout(resting);
                    resting = window.setTimeout(() => slot.classList.remove("is-walking"), 200);
                }

                last = at;

                const near = Math.round(at);

                if (near !== here) {
                    here = near;
                    stops.forEach((item, i) => item.classList.toggle("is-here", i === near));
                    steps.forEach((step, i) => step.classList.toggle("is-on", i <= near));

                    if (line) {
                        line.textContent = stops[near].querySelector("figcaption")?.textContent ?? line.textContent;
                        bubble?.classList.remove("is-new");
                        void bubble?.offsetWidth;
                        bubble?.classList.add("is-new");
                    }
                }
            });
        });

        changes.classList.add("is-staged");

        const travel = () => Math.max(1, changes.offsetHeight - window.innerHeight);

        const show = (index) => {
            if (index === current) {
                return;
            }

            current = index;

            slots.forEach((slot, i) => {
                slot.classList.toggle("is-current", i === index);
                slot.classList.toggle("is-past", i < index);
            });

            // The stage takes the look's colours: the dark behind its photo and its accent.
            const slot = slots[index];
            stage.style.setProperty("--stage-bg", slot.style.getPropertyValue("--look-stage"));
            stage.style.setProperty("--stage-hot", slot.style.getPropertyValue("--look-hot"));

            refreshLooks();
        };

        const update = () => {
            queued = false;
            const progress = Math.min(1, Math.max(0, -changes.getBoundingClientRect().top / travel()));
            const at = Math.min(total - 0.0001, progress * total);
            let index = slots.length - 1;

            while (index > 0 && starts[index] > at) {
                index--;
            }

            show(index);
            tours.get(slots[index])?.((at - starts[index]) / weights[index]);
        };

        const queue = () => {
            if (!queued) {
                queued = true;
                requestAnimationFrame(update);
            }
        };

        // A #look-… link goes to the middle of that character's stretch (to the start of a
        // tour).
        const goTo = (index, behavior) => {
            const top = changes.getBoundingClientRect().top + window.scrollY + travel() * (starts[index] + 0.5) / total;
            window.scrollTo({ top, behavior });
        };

        const followHash = () => {
            const linked = slots.findIndex((slot) => "#" + slot.id === location.hash);

            if (linked >= 0) {
                goTo(linked, "auto");
            }
        };

        // After the browser's own jump to the anchor, which lands on the top of the stage.
        window.addEventListener("load", () => requestAnimationFrame(followHash));
        window.addEventListener("hashchange", followHash);

        window.addEventListener("scroll", queue, { passive: true });
        window.addEventListener("resize", queue);
        update();
    }
})();
