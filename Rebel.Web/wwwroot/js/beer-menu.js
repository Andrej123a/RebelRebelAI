// Beer menu stage: the stage takes the colours of the track crossing the middle
// of the screen, the tracklist follows along, and the filters hide tracks.
(() => {
    const stage = document.querySelector("[data-beer-stage]");

    if (!stage) {
        return;
    }

    const tracks = Array.from(stage.querySelectorAll("[data-beer-track]"));
    const sides = Array.from(stage.querySelectorAll("[data-beer-side]"));
    const rail = stage.querySelector("[data-beer-rail-scroller]");
    const railGroups = Array.from(stage.querySelectorAll("[data-beer-rail-group]"));
    const railItems = new Map(
        Array.from(stage.querySelectorAll("[data-beer-rail]"))
            .map((item) => [item.dataset.beerRail, item]));

    const tools = document.querySelector("[data-beer-tools]");
    const searchInput = document.querySelector("[data-beer-search]");
    const familyButtons = Array.from(document.querySelectorAll("[data-beer-family]"));
    const availableOnly = document.querySelector("[data-beer-available-only]");
    const summary = document.querySelector("[data-beer-summary]");
    const emptyState = document.querySelector("[data-beer-empty]");
    const clearButton = document.querySelector("[data-beer-clear]");

    const reducedMotion = window.matchMedia("(prefers-reduced-motion: reduce)");

    let activeTrack = null;
    let activeFamily = "all";
    let jumpLockedUntil = 0;

    const scrollBehavior = () => (reducedMotion.matches ? "auto" : "smooth");

    const keepRailItemVisible = (item) => {
        if (!rail || !item) {
            return;
        }

        const railBox = rail.getBoundingClientRect();
        const itemBox = item.getBoundingClientRect();
        const scrollsSideways = rail.scrollWidth > rail.clientWidth + 1;

        if (scrollsSideways) {
            if (itemBox.left < railBox.left + 24 || itemBox.right > railBox.right - 24) {
                rail.scrollTo({
                    left: rail.scrollLeft + itemBox.left - railBox.left - (railBox.width - itemBox.width) / 2,
                    behavior: scrollBehavior()
                });
            }

            return;
        }

        if (itemBox.top < railBox.top + 48 || itemBox.bottom > railBox.bottom - 48) {
            rail.scrollTo({
                top: rail.scrollTop + itemBox.top - railBox.top - (railBox.height - itemBox.height) / 2,
                behavior: scrollBehavior()
            });
        }
    };

    const setActive = (track) => {
        if (!track || track === activeTrack) {
            return;
        }

        if (activeTrack) {
            activeTrack.classList.remove("is-active");

            const previousItem = railItems.get(activeTrack.id);
            previousItem?.classList.remove("is-active");
            previousItem?.querySelector("a")?.removeAttribute("aria-current");
        }

        activeTrack = track;
        track.classList.add("is-active");
        stage.style.setProperty("--stage-bg", track.dataset.bg);
        stage.style.setProperty("--stage-ink", track.dataset.ink);

        const item = railItems.get(track.id);
        item?.classList.add("is-active");
        item?.querySelector("a")?.setAttribute("aria-current", "true");
        keepRailItemVisible(item);
    };

    // The track whose box spans the middle of the viewport owns the stage.
    // Between tracks (a side heading) the previous owner keeps it.
    const findCentredTrack = () => {
        const middle = window.innerHeight / 2;

        return tracks.find((track) => {
            if (track.hidden) {
                return false;
            }

            const box = track.getBoundingClientRect();
            return box.top <= middle && box.bottom >= middle;
        });
    };

    const syncStage = () => {
        if (performance.now() < jumpLockedUntil) {
            return;
        }

        const centred = findCentredTrack();

        if (centred) {
            setActive(centred);
            return;
        }

        if (!activeTrack || activeTrack.hidden) {
            setActive(tracks.find((track) => !track.hidden));
        }
    };

    let syncQueued = false;

    const queueSync = () => {
        if (syncQueued) {
            return;
        }

        syncQueued = true;
        requestAnimationFrame(() => {
            syncQueued = false;
            syncStage();
        });
    };

    const releaseJumpLock = () => {
        jumpLockedUntil = 0;
        queueSync();
    };

    // A tracklist jump scrolls past every beer in between; painting each of them
    // would flash the whole stage, so the target takes the stage straight away.
    stage.addEventListener("click", (event) => {
        const link = event.target.closest("[data-beer-rail] a");

        if (!link) {
            return;
        }

        const target = document.getElementById(link.hash.slice(1));

        if (!target || target.hidden) {
            return;
        }

        jumpLockedUntil = performance.now() + 1500;
        setActive(target);
        target.classList.add("is-in-view");
    });

    if ("onscrollend" in window) {
        window.addEventListener("scrollend", () => {
            if (jumpLockedUntil) {
                releaseJumpLock();
            }
        });
    }

    window.addEventListener("scroll", queueSync, { passive: true });
    window.addEventListener("resize", queueSync);

    if ("IntersectionObserver" in window) {
        const entrances = new IntersectionObserver(
            (entries) => {
                entries.forEach((entry) => {
                    if (entry.isIntersecting) {
                        entry.target.classList.add("is-in-view");
                        entrances.unobserve(entry.target);
                    }
                });
            },
            { threshold: 0.2 });

        tracks.forEach((track) => entrances.observe(track));
    } else {
        tracks.forEach((track) => track.classList.add("is-in-view"));
    }

    // ---------------------------------------------------------------
    // Filters
    // ---------------------------------------------------------------

    const applyFilters = () => {
        const terms = (searchInput?.value ?? "")
            .trim()
            .toLowerCase()
            .split(/\s+/)
            .filter(Boolean);
        const onlyAvailable = availableOnly?.checked ?? false;

        let visibleCount = 0;

        tracks.forEach((track) => {
            const search = track.dataset.search ?? "";
            const isVisible =
                terms.every((term) => search.includes(term)) &&
                (activeFamily === "all" || track.dataset.family === activeFamily) &&
                (!onlyAvailable || track.dataset.available === "true");

            track.hidden = !isVisible;

            const item = railItems.get(track.id);

            if (item) {
                item.hidden = !isVisible;
            }

            if (isVisible) {
                visibleCount++;
            }
        });

        sides.forEach((side) => {
            side.hidden = side.querySelector("[data-beer-track]:not([hidden])") === null;
        });

        railGroups.forEach((group) => {
            group.hidden = group.querySelector("[data-beer-rail]:not([hidden])") === null;
        });

        if (summary) {
            summary.textContent = visibleCount === tracks.length
                ? `${tracks.length} ${tracks.length === 1 ? "beer" : "beers"} on the record`
                : `${visibleCount} of ${tracks.length} beers`;
        }

        if (emptyState) {
            emptyState.hidden = visibleCount > 0;
        }

        stage.hidden = visibleCount === 0;

        queueSync();
    };

    const setFamily = (family) => {
        activeFamily = family;

        familyButtons.forEach((button) => {
            const isActive = button.dataset.beerFamily === family;
            button.classList.toggle("is-active", isActive);
            button.setAttribute("aria-pressed", isActive ? "true" : "false");
        });
    };

    familyButtons.forEach((button) => {
        button.addEventListener("click", () => {
            setFamily(button.dataset.beerFamily ?? "all");
            applyFilters();
        });
    });

    searchInput?.addEventListener("input", applyFilters);
    availableOnly?.addEventListener("change", applyFilters);

    clearButton?.addEventListener("click", () => {
        if (searchInput) {
            searchInput.value = "";
        }

        if (availableOnly) {
            availableOnly.checked = false;
        }

        setFamily("all");
        applyFilters();
        searchInput?.focus();
    });

    if (tools) {
        tools.hidden = false;
    }

    stage.classList.add("is-live");
    applyFilters();
    syncStage();
})();
