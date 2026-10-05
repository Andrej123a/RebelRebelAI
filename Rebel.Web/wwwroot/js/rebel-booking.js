// Booking a table is a talk with Major Tom (Views/Reservations/Create.cshtml):
// - he asks one thing at a time (a moment of "..." first, as if over the radio): the
//   night (for an event he already knows it), the time, how many, a name, a number,
//   and, for a crew of 13 or more, a word about it,
// - you answer in the field under his question,
// - your answers stay in the log, and tapping one takes you back to change it,
// - at the end he reads the flight plan back and you send it up.
// The fields are the form's own, so the server checks them as before. When the server
// sends the form back with a problem, he picks the talk up at that question and says
// what it was. Without this script every question is open at once.
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
    const field = (step) => step.querySelector("input:not([type=hidden]), textarea") ?? step.querySelector("input[type=hidden]");
    const value = (step) => (field(step)?.value ?? "").trim();
    const crew = () => Number(value(named("crew"))) || 0;
    const big = () => crew() >= 13;
    const shown = new Set();
    let touched = false;

    // ---------- what you said, read back ----------

    const nightName = (iso) => (iso
        ? new Date(`${iso}T12:00`).toLocaleDateString("en-GB", { weekday: "short", day: "numeric", month: "short" })
        : "");

    const echo = {
        date: (step) => nightName(value(step)),
        time: (step) => value(step).slice(0, 5),
        crew: () => (crew() === 1 ? "Just me" : `${crew()} of us`),
        name: (step) => value(step),
        phone: (step) => value(step),
        note: (step) => value(step),
    };

    const readBack = () => {
        if (!plan) {
            return;
        }

        const night = nightName(value(named("date")));
        const time = value(named("time")).slice(0, 5);
        const party = crew() === 1 ? "just you" : `${crew()} of you`;
        const event = talk.dataset.rsEvent ? ` for ${talk.dataset.rsEvent}` : "";

        plan.textContent = `${night} at ${time}${event}, ${party}, under ${value(named("name"))}. Shall I send it up to Ground Control? I'll radio back a code so you can follow it.`;
    };

    // ---------- when he has to ask again ----------

    const complain = (step, message) => {
        let trouble = step.querySelector(".rs-talk-trouble");

        if (!message) {
            trouble?.remove();
            return;
        }

        if (!trouble) {
            trouble = document.createElement("p");
            trouble.className = "rs-talk-tom rs-talk-trouble";
            step.querySelector(".rs-talk-tom").after(trouble);
        }

        trouble.textContent = message;
    };

    // The same rule the server keeps: at least two hours from now (Skopje time).
    const [earliestNight, earliestTime] = (talk.dataset.rsEarliest || "T").split("T");

    const tooSoon = () => {
        const night = value(named("date"));
        const time = value(named("time")).slice(0, 5);

        return Boolean(night && time && earliestNight) &&
            (night < earliestNight || (night === earliestNight && time < earliestTime));
    };

    const valid = (step) => {
        const input = field(step);

        if (!input || input.type === "hidden") {
            return true;
        }

        if (step.dataset.rsStep === "date" && value(step) && earliestNight && value(step) < earliestNight) {
            complain(step, "That night has already gone. Pick one from today on.");
            return false;
        }

        if (step.dataset.rsStep === "time" && tooSoon()) {
            complain(step, "That's too soon for me: at least two hours from now, please.");
            return false;
        }

        if (step.dataset.rsStep === "note" && big() && !value(step)) {
            complain(step, "Just a word or two, so we can seat you all.");
            input.focus();
            return false;
        }

        const $ = window.jQuery;
        const ok = $ && $.fn && $.fn.valid && $(input).closest("form").data("validator")
            ? $(input).valid()
            : input.checkValidity() && (Boolean(value(step)) || step.dataset.rsStep === "note");

        if (ok) {
            complain(step, "");
        }

        return ok;
    };

    // ---------- moving through the talk ----------

    // The note is only asked of a crew of 13 or more.
    const skipped = (step) => step.dataset.rsStep === "note" && !big();

    const say = (step) => {
        if (said) {
            said.textContent = step.querySelector(".rs-talk-words")?.innerText.trim() ?? "";
        }
    };

    const goTo = (index) => {
        const step = steps[index];

        named("note").classList.toggle("is-big", big());
        named("note").classList.toggle("is-skipped", !big());

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
                step.querySelector(".rs-talk-type input, .rs-talk-type textarea, button[type=submit]")?.focus({ preventScroll: true });
            }, reducedMotion ? 0 : 700);
        }
    };

    const next = (index) => {
        const found = steps.findIndex((item, i) =>
            i > index && item.dataset.rsStep !== "send" && !item.classList.contains("is-answered") && !skipped(item));

        return found === -1 ? steps.length - 1 : found;
    };

    const answer = (index) => {
        const step = steps[index];

        if (!valid(step)) {
            return;
        }

        touched = true;
        step.classList.add("is-answered");
        step.querySelector("[data-rs-you-said]").textContent = echo[step.dataset.rsStep](step);

        // A new night can make the time you gave too soon: he asks for it again.
        if (step.dataset.rsStep === "date" && named("time").classList.contains("is-answered") && tooSoon()) {
            named("time").classList.remove("is-answered");
        }

        goTo(next(index));
    };

    // ---------- the answers ----------

    steps.forEach((step, index) => {
        const input = field(step);

        step.querySelectorAll("[data-rs-send]").forEach((send) => send.addEventListener("click", () => answer(index)));

        // Enter answers him instead of sending the whole form.
        if (input && input.tagName !== "TEXTAREA" && input.type !== "hidden") {
            input.addEventListener("keydown", (event) => {
                if (event.key === "Enter") {
                    event.preventDefault();
                    answer(index);
                }
            });
        }

        step.querySelector("[data-rs-you]")?.addEventListener("click", () => {
            touched = true;
            goTo(index);
        });
    });

    // ---------- where the talk starts ----------

    talk.classList.add("is-talking");

    // What he already knows (an event's night) is in the log from the start.
    steps.filter((step) => step.hasAttribute("data-rs-fixed")).forEach((step) => {
        step.classList.add("is-answered");
        shown.add(step);
    });

    const problem = steps.findIndex((step) =>
        step.querySelector(".field-validation-error:not(:empty), .input-validation-error"));
    const sentBack = problem !== -1 || talk.querySelector(".validation-summary-errors");

    if (sentBack) {
        // The server sent it back: everything you said is in the log, and he asks again
        // where the problem is, saying what it was.
        steps.forEach((step) => {
            if (step.dataset.rsStep !== "send" && !skipped(step) && value(step)) {
                step.classList.add("is-answered");
                step.querySelector("[data-rs-you-said]").textContent = echo[step.dataset.rsStep](step);
            }

            shown.add(step);
        });

        if (problem !== -1) {
            const message = steps[problem].querySelector(".field-validation-error")?.textContent.trim();
            const fixed = steps[problem].hasAttribute("data-rs-fixed");

            if (message && !fixed) {
                complain(steps[problem], message);
            }

            // A problem with the event's own night is said there, then he waits at the send.
            goTo(fixed ? steps.length - 1 : problem);
        } else {
            goTo(steps.length - 1);
        }
    } else {
        goTo(next(-1));
    }
})();
