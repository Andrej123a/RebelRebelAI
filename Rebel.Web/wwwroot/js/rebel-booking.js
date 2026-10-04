// Booking a table is a talk with Major Tom (Views/Reservations/Create.cshtml):
// - he asks one thing at a time (a moment of "..." first, as if over the radio),
// - you answer by picking (a night, a time, how many) or by typing (name, phone, a note),
// - your answers stay in the log, and tapping one takes you back to change it,
// - at the end he reads the flight plan back and you send it up.
// The answers fill the form's own fields, so the server checks them as before. When the
// server sends the form back with a problem, he picks the talk up at that question.
// Without this script every question is open at once.
(() => {
    const talk = document.querySelector("[data-rs-talk]");

    if (!talk) {
        return;
    }

    const reducedMotion = window.matchMedia("(prefers-reduced-motion: reduce)").matches;
    const steps = Array.from(talk.querySelectorAll("[data-rs-step]"));
    const said = talk.querySelector("[data-rs-said]");
    const plan = talk.querySelector("[data-rs-plan]");
    const named = (name) => steps.find((step) => step.dataset.rsStep === name);
    const field = (step) => step.querySelector("input:not([type=hidden]), textarea");
    const value = (step) => (field(step)?.value ?? "").trim();
    const shown = new Set();
    let touched = false;

    // ---------- what you said, read back ----------

    const nightName = (iso) => {
        const pick = named("date").querySelector(`[data-rs-pick="${iso}"]`);

        return pick
            ? pick.textContent.trim()
            : new Date(`${iso}T12:00`).toLocaleDateString("en-GB", { weekday: "short", day: "numeric", month: "short" });
    };

    const crewName = (count, who) => (count === 1 ? `just ${who}` : `${count} of ${who === "me" ? "us" : "you"}`);

    const echo = {
        date: (step) => nightName(value(step)),
        time: (step) => value(step).slice(0, 5),
        crew: (step) => {
            const words = crewName(Number(value(step)), "me");
            return words.charAt(0).toUpperCase() + words.slice(1);
        },
        name: (step) => value(step),
        phone: (step) => value(step),
        note: (step) => value(step) || "No, that's all",
    };

    // The form's own value, in the shape the picks carry it.
    const current = (step) => (step.dataset.rsStep === "time" ? value(step).slice(0, 5) : value(step));

    const mark = (step) => {
        step.querySelectorAll("[data-rs-pick]").forEach((pick) => {
            const on = pick.dataset.rsPick === current(step);
            pick.classList.toggle("is-picked", on);
            pick.setAttribute("aria-pressed", on ? "true" : "false");
        });

        // A value none of the picks carries is shown in its own field.
        const picks = step.querySelector("[data-rs-pick]");

        if (picks && value(step) && !step.querySelector("[data-rs-pick].is-picked")) {
            step.classList.add("is-other");
        }
    };

    // ---------- arrival times that are too soon ----------

    const [earliestNight, earliestTime] = (talk.dataset.rsEarliest || "T").split("T");

    const greyOut = () => {
        const night = value(named("date"));
        const timeStep = named("time");

        timeStep.querySelectorAll("[data-rs-pick]").forEach((pick) => {
            pick.disabled = Boolean(earliestTime) && night === earliestNight && pick.dataset.rsPick < earliestTime;
        });

        named("date").querySelectorAll("[data-rs-pick]").forEach((pick) => {
            pick.disabled = Boolean(earliestNight) && pick.dataset.rsPick < earliestNight;
        });

        // A time that is now too soon has to be asked again.
        if (timeStep.querySelector("[data-rs-pick].is-picked:disabled")) {
            field(timeStep).value = "";
            timeStep.classList.remove("is-answered");
            mark(timeStep);
        }
    };

    // ---------- checking an answer ----------

    const valid = (step) => {
        const input = field(step);

        if (!input) {
            return true;
        }

        if (step.dataset.rsStep === "note") {
            // A crew of 13 or more has to say a word about it (the server asks the same).
            const big = Number(value(named("crew"))) >= 13;

            if (big && !value(step)) {
                input.focus();
                return false;
            }
        }

        const $ = window.jQuery;

        if ($ && $.fn && $.fn.valid && $(input).closest("form").data("validator")) {
            return $(input).valid();
        }

        return input.checkValidity();
    };

    // ---------- moving through the talk ----------

    const say = (step) => {
        if (said) {
            said.textContent = step.querySelector(".rs-talk-words")?.innerText.trim() ?? "";
        }
    };

    const readBack = () => {
        if (!plan) {
            return;
        }

        const name = value(named("name"));
        const night = nightName(value(named("date")));
        const time = value(named("time")).slice(0, 5);
        const crew = crewName(Number(value(named("crew"))), "you");

        plan.textContent = `${night} at ${time}, ${crew}, under ${name}. Shall I send it up to Ground Control? I'll radio back a code so you can follow it.`;
    };

    const goTo = (index) => {
        const step = steps[index];

        if (step.dataset.rsStep === "note") {
            step.classList.toggle("is-big", Number(value(named("crew"))) >= 13);
        }

        if (step.dataset.rsStep === "send") {
            readBack();
        }

        steps.forEach((item, i) => item.classList.toggle("is-current", i === index));

        // The first time he asks it, he takes a moment, as if over the radio.
        if (!shown.has(step) && touched && !reducedMotion) {
            step.classList.add("is-typing");
            window.setTimeout(() => step.classList.remove("is-typing"), 650);
        }

        shown.add(step);
        say(step);

        if (touched) {
            step.scrollIntoView({ block: "nearest", behavior: reducedMotion ? "auto" : "smooth" });

            window.setTimeout(() => {
                const first = step.querySelector(".rs-talk-type input, .rs-talk-type textarea, [data-rs-pick].is-picked, [data-rs-pick]:not(:disabled), button[type=submit]");
                first?.focus({ preventScroll: true });
            }, reducedMotion ? 0 : 700);
        }
    };

    const answer = (index) => {
        const step = steps[index];

        if (!valid(step)) {
            return;
        }

        touched = true;
        step.classList.add("is-answered");
        step.querySelector("[data-rs-you-said]").textContent = echo[step.dataset.rsStep](step);

        if (step.dataset.rsStep === "date") {
            greyOut();
        }

        const next = steps.findIndex((item, i) => i > index && !item.classList.contains("is-answered") && item.dataset.rsStep !== "send");
        goTo(next === -1 ? steps.length - 1 : next);
    };

    // ---------- the answers ----------

    steps.forEach((step, index) => {
        const input = field(step);

        step.querySelectorAll("[data-rs-pick]").forEach((pick) => {
            pick.addEventListener("click", () => {
                input.value = pick.dataset.rsPick;
                input.dispatchEvent(new Event("change", { bubbles: true }));
                step.classList.remove("is-other");
                mark(step);
                answer(index);
            });
        });

        step.querySelector("[data-rs-other]")?.addEventListener("click", () => {
            step.classList.add("is-other");
            input.focus();
        });

        step.querySelectorAll("[data-rs-send]").forEach((send) => send.addEventListener("click", () => answer(index)));

        step.querySelector("[data-rs-skip]")?.addEventListener("click", () => {
            input.value = "";
            answer(index);
        });

        // Enter answers him instead of sending the whole form.
        if (input && input.tagName !== "TEXTAREA") {
            input.addEventListener("keydown", (event) => {
                if (event.key === "Enter") {
                    event.preventDefault();
                    answer(index);
                }
            });
        }

        if (step.dataset.rsStep === "date") {
            input?.addEventListener("change", greyOut);
        }

        step.querySelector("[data-rs-you]")?.addEventListener("click", () => {
            touched = true;
            goTo(index);
        });

        mark(step);
    });

    // ---------- where the talk starts ----------

    talk.classList.add("is-talking");
    greyOut();

    const problem = steps.findIndex((step) =>
        step.querySelector(".field-validation-error:not(:empty), .input-validation-error"));
    const sentBack = problem !== -1 || talk.querySelector(".validation-summary-errors");

    if (sentBack) {
        // The server sent it back: everything you said is in the log, and he asks again
        // where the problem is (or reads the plan back if it is about the whole booking).
        steps.forEach((step) => {
            if (step.dataset.rsStep !== "send" && (value(step) || step.dataset.rsStep === "note")) {
                step.classList.add("is-answered");
                step.querySelector("[data-rs-you-said]").textContent = echo[step.dataset.rsStep](step);
            }
        });

        // He says what the trouble is himself (the field it belongs to may be tucked away
        // behind the picks).
        if (problem !== -1) {
            const message = steps[problem].querySelector(".field-validation-error")?.textContent.trim();

            if (message) {
                const trouble = document.createElement("p");
                trouble.className = "rs-talk-tom rs-talk-trouble";
                trouble.textContent = message;
                steps[problem].querySelector(".rs-talk-tom").after(trouble);
            }
        }

        steps.forEach((step) => shown.add(step));
        goTo(problem !== -1 ? problem : steps.length - 1);
    } else {
        goTo(0);
    }
})();
