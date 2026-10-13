// Lets the loading dive end on a whole drawing instead of being cut off mid-stroke when the app
// appears. The times are the dive animation's: a 0.5 s delay, a 2.6 s loop, the ceiling whole by 1.9 s.
(() => {
    const screen = document.getElementById("loading");
    const app = document.getElementById("app");
    if (!screen || !app) {
        return;
    }

    const delay = 500;
    const loop = 2600;
    const whole = 1900;
    const barelyStarted = 400;

    const leave = () => {
        screen.classList.add("loading-done");
        setTimeout(() => screen.remove(), 300);
    };

    const appeared = () => {
        const drawing = screen.querySelector(".loading-dive-profile")?.getAnimations()[0];
        const elapsed = typeof drawing?.currentTime === "number" ? drawing.currentTime - delay : -1;
        if (elapsed < 0) {
            screen.remove();
            return;
        }

        const intoLoop = elapsed % loop;
        if (intoLoop < barelyStarted || intoLoop >= whole) {
            leave();
        } else {
            setTimeout(leave, whole - intoLoop);
        }
    };

    const watch = new MutationObserver(() => {
        if (app.childElementCount > 0) {
            watch.disconnect();
            appeared();
        }
    });
    watch.observe(app, { childList: true });
})();
