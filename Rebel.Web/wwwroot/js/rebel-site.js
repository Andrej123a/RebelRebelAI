// Shared motion for the public pages:
// - the Space Oddity countdown preloader (once per visit),
// - the full-screen #open menu,
// - blocks marked data-rs-reveal come into focus as they scroll in,
// - blocks marked data-rs-drift move at their own speed while scrolling,
// - the HUD: the mission clock and the altitude tape,
// - Bowie's looks (Views/Shared/Looks) come alive while on screen,
// - the home page's Changes stage, one look at a time while you scroll.
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

    // ---------- HUD: the mission clock and the altitude tape ----------

    const clocks = document.querySelectorAll("[data-rs-clock]");

    if (clocks.length) {
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

        // The tape's marker climbs as you scroll down the page. Set on the HUDs only, so
        // a scroll restyles them and not the whole page.
        const huds = document.querySelectorAll(".rs-hud");
        let pending = false;

        const climb = () => {
            pending = false;
            const travel = Math.max(1, root.scrollHeight - window.innerHeight);
            const height = (Math.min(1, window.scrollY / travel)).toFixed(3);
            huds.forEach((hud) => hud.style.setProperty("--hud-tape", height));
        };

        window.addEventListener("scroll", () => {
            if (!pending) {
                pending = true;
                requestAnimationFrame(climb);
            }
        }, { passive: true });
        climb();
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

    // ---------- Bowie's looks: the ink boils only while a look is on screen ----------

    const looks = Array.from(document.querySelectorAll(".rs-look"));
    const onScreen = new Set();
    let refreshLooks = () => {};

    if (looks.length && "IntersectionObserver" in window) {
        root.classList.add("rs-looks-on");

        // On the Changes stage only the look in the spotlight is live, and its plates
        // land again every time it comes back; elsewhere they land once.
        const refresh = (look) => {
            const slot = look.closest("[data-rs-look]");
            const staged = Boolean(slot && slot.closest(".is-staged"));
            const live = onScreen.has(look) && (!staged || slot.classList.contains("is-current"));

            look.classList.toggle("is-live", live);

            if (staged) {
                look.classList.toggle("is-seen", live);
            } else if (live) {
                look.classList.add("is-seen");
            }
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
        const roll = Array.from(changes.querySelectorAll("[data-rs-roll]"));
        const counter = changes.querySelector("[data-rs-changes-now]");
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

            roll.forEach((link, i) => {
                link.classList.toggle("is-current", i === index);
                link.classList.toggle("is-past", i < index);
            });

            // The stage takes the look's colours: its backdrop, its ink, its accent.
            const slot = slots[index];
            stage.style.setProperty("--stage-bg", slot.style.getPropertyValue("--look-stage"));
            stage.style.setProperty("--stage-ink", slot.style.getPropertyValue("--look-ink"));
            stage.style.setProperty("--stage-hot", slot.style.getPropertyValue("--look-hot"));

            if (counter) {
                counter.textContent = String(index + 1).padStart(2, "0");
            }

            refreshLooks();
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

        // A year on the roll (or a #look-… link) goes to the middle of that look's stretch.
        const goTo = (index, behavior) => {
            const top = changes.getBoundingClientRect().top + window.scrollY + travel() * (index + 0.5) / slots.length;
            window.scrollTo({ top, behavior });
        };

        roll.forEach((link, i) => {
            link.addEventListener("click", (event) => {
                event.preventDefault();
                goTo(i, "smooth");
            });
        });

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
