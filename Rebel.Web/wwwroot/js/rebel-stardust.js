// Shared motion for the public pages: headings marked data-rs-flip flip in
// letter by letter, blocks marked data-rs-reveal slide in once they are seen.
(() => {
    const reducedMotion = window.matchMedia("(prefers-reduced-motion: reduce)").matches;

    // Wrap every word of a heading in a no-wrap span and every letter in its own
    // span, leaving gradient text alone (background-clip breaks on split letters).
    const splitLetters = (heading) => {
        if (!heading.hasAttribute("aria-label")) {
            heading.setAttribute("aria-label", heading.textContent.replace(/\s+/g, " ").trim());
        }

        let index = 0;

        const walk = (node) => {
            Array.from(node.childNodes).forEach((child) => {
                if (child.nodeType === Node.ELEMENT_NODE) {
                    if (!child.classList.contains("rs-gradient-text") && !child.hasAttribute("data-rs-noflip")) {
                        walk(child);
                    }

                    child.setAttribute("aria-hidden", "true");
                    return;
                }

                if (child.nodeType !== Node.TEXT_NODE || !child.textContent.trim()) {
                    return;
                }

                const fragment = document.createDocumentFragment();

                child.textContent.split(/(\s+)/).forEach((part) => {
                    if (!part) {
                        return;
                    }

                    if (/^\s+$/.test(part)) {
                        fragment.append(document.createTextNode(" "));
                        return;
                    }

                    const word = document.createElement("span");
                    word.className = "rs-flip-word";
                    word.setAttribute("aria-hidden", "true");

                    Array.from(part).forEach((character) => {
                        const letter = document.createElement("span");
                        letter.className = "rs-flip-letter";
                        letter.style.setProperty("--i", String(index++));
                        letter.textContent = character;
                        word.append(letter);
                    });

                    fragment.append(word);
                });

                child.replaceWith(fragment);
            });
        };

        walk(heading);
        heading.classList.add("rs-flip-ready");
    };

    if (!reducedMotion) {
        document.querySelectorAll("[data-rs-flip]").forEach(splitLetters);
    }

    const targets = document.querySelectorAll("[data-rs-flip], [data-rs-reveal]");

    if (reducedMotion || !("IntersectionObserver" in window)) {
        targets.forEach((target) => target.classList.add("is-in-view"));
        return;
    }

    const observer = new IntersectionObserver(
        (entries) => {
            entries.forEach((entry) => {
                if (entry.isIntersecting) {
                    entry.target.classList.add("is-in-view");
                    observer.unobserve(entry.target);
                }
            });
        },
        { threshold: 0.15 });

    targets.forEach((target) => observer.observe(target));
})();
