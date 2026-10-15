// Runs in the page head, before the first paint, so a dark theme never flashes light.
(() => {
    const key = "bitenovac.theme";
    const system = matchMedia("(prefers-color-scheme: dark)");

    const read = () => {
        try {
            return localStorage.getItem(key) ?? "light";
        } catch {
            return "light";
        }
    };

    const apply = choice => {
        const dark = choice === "dark" || (choice === "system" && system.matches);
        document.documentElement.dataset.theme = dark ? "dark" : "light";
    };

    system.addEventListener("change", () => apply(read()));
    apply(read());

    window.bitenovacTheme = {
        set(choice) {
            localStorage.setItem(key, choice);
            apply(choice);
        }
    };
})();
