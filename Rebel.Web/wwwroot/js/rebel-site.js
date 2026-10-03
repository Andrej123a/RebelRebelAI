// Shared motion for the public pages:
// - the Space Oddity countdown preloader (once per visit),
// - the full-screen #open menu,
// - blocks marked data-rs-reveal come into focus as they scroll in,
// - blocks marked data-rs-drift move at their own speed while scrolling,
// - the HUD readouts: mission clock, altitude and speed.
// The night sky, falling stars and stardust live in rebel-sky.js.
// Without JS or with reduced motion everything is simply shown.
(() => {
    const root = document.documentElement;
    const reducedMotion = window.matchMedia("(prefers-reduced-motion: reduce)").matches;

    // ---------- preloader: a Space Oddity countdown ----------

    const preloader = document.querySelector("[data-rs-preloader]");

    if (preloader && !root.classList.contains("rs-seen")) {
        const counter = preloader.querySelector("[data-rs-count]");
        const status = preloader.querySelector("[data-rs-status]");
        const finish = () => {
            preloader.classList.add("is-done");
            window.setTimeout(() => { preloader.hidden = true; }, 900);
        };

        if (reducedMotion || !counter) {
            preloader.hidden = true;
        } else {
            const started = performance.now();
            const duration = 1100;
            const tick = (now) => {
                const progress = Math.min(1, Math.max(0, (now - started) / duration));
                counter.textContent = String(10 - Math.floor(progress * 10));

                if (progress < 1) {
                    requestAnimationFrame(tick);
                    return;
                }

                counter.textContent = "0";

                if (status) {
                    status.textContent = "Lift-off";
                }

                preloader.classList.add("is-lift");
                window.setTimeout(finish, 380);
            };

            requestAnimationFrame(tick);
        }
    }

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

    // ---------- HUD: mission clock, altitude and speed ----------

    const clocks = document.querySelectorAll("[data-rs-clock]");
    const altitudes = document.querySelectorAll("[data-rs-alt]");
    const speeds = document.querySelectorAll("[data-rs-vel]");

    if (clocks.length || altitudes.length || speeds.length) {
        // The clock runs from the first page of the visit, like a mission clock from lift-off.
        let launched = Date.now();

        try {
            launched = Number(sessionStorage.getItem("rs-t0")) || launched;
            sessionStorage.setItem("rs-t0", String(launched));
        } catch {
            // Private mode: the clock starts on every page instead.
        }

        const two = (n) => String(n).padStart(2, "0");
        const tick = () => {
            const s = Math.max(0, Math.floor((Date.now() - launched) / 1000));
            const text = `${two(Math.floor(s / 3600))}:${two(Math.floor(s / 60) % 60)}:${two(s % 60)}`;
            clocks.forEach((clock) => { clock.textContent = text; });
        };

        tick();
        window.setInterval(tick, 1000);

        // Scrolling down the page climbs; scrolling fast adds speed, which bleeds off again.
        let lastY = window.scrollY;
        let lastAt = performance.now();
        let boost = 0;
        let pending = false;

        const telemetry = (now) => {
            pending = false;
            const y = window.scrollY;
            const rate = Math.abs(y - lastY) / Math.max(16, now - lastAt);
            lastY = y;
            lastAt = now;
            boost = boost * 0.9 + rate * 0.1;

            const travel = Math.max(1, root.scrollHeight - window.innerHeight);
            root.style.setProperty("--hud-tape", (Math.min(1, y / travel)).toFixed(3));

            const altitude = Math.round(408 + y * 0.12).toLocaleString("en-US");
            altitudes.forEach((alt) => { alt.textContent = altitude; });

            const speed = (7.66 + boost * 3).toFixed(2);
            speeds.forEach((vel) => { vel.textContent = speed; });

            if (boost > 0.002) {
                pending = true;
                requestAnimationFrame(telemetry);
            }
        };

        const queue = () => {
            if (!pending) {
                pending = true;
                requestAnimationFrame(telemetry);
            }
        };

        window.addEventListener("scroll", queue, { passive: true });
        queue();
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
})();
