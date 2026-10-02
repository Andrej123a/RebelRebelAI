// Shared motion for the public pages: blocks marked data-rs-reveal fade in
// once they scroll into view. Nothing hides without JS or with reduced motion.
(() => {
    const reducedMotion = window.matchMedia("(prefers-reduced-motion: reduce)").matches;
    const targets = document.querySelectorAll("[data-rs-reveal]");

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
