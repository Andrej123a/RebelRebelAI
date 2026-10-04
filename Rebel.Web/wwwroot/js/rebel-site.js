// Shared motion for the public pages:
// - the full-screen #open menu,
// - blocks marked data-rs-reveal come into focus as they scroll in,
// - blocks marked data-rs-drift move at their own speed while scrolling,
// - the home page's "How Rebel works" stage, one photo at a time while you scroll.
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

    // ---------- Ch-ch-changes: the stage shows one part of the pub at a time ----------

    const changes = document.querySelector("[data-rs-changes]");

    if (changes) {
        const slots = Array.from(changes.querySelectorAll("[data-rs-look]"));
        const stage = changes.querySelector(".rs-changes-stage");
        let current = -1;
        let queued = false;

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
        };

        const update = () => {
            queued = false;
            const progress = Math.min(1, Math.max(0, -changes.getBoundingClientRect().top / travel()));
            show(Math.min(slots.length - 1, Math.floor(progress * slots.length)));
        };

        const queue = () => {
            if (!queued) {
                queued = true;
                requestAnimationFrame(update);
            }
        };

        // A #look-… link goes to the middle of that character's stretch.
        const goTo = (index, behavior) => {
            const top = changes.getBoundingClientRect().top + window.scrollY + travel() * (index + 0.5) / slots.length;
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
