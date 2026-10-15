const listeners = new Set();
const updateCheckInterval = 60 * 60 * 1000;

let installPrompt = null;
let waitingWorker = null;
let updating = false;
let installDismissed = false;

const notify = () => listeners.forEach(listener => listener());

const setWaiting = worker => {
    waitingWorker = worker;
    worker.addEventListener("statechange", () => {
        if (waitingWorker === worker && (worker.state === "activated" || worker.state === "redundant")) {
            waitingWorker = null;
            notify();
        }
    });
    notify();
};

const watch = registration => {
    if (registration.waiting && navigator.serviceWorker.controller) {
        setWaiting(registration.waiting);
    }

    registration.addEventListener("updatefound", () => {
        const worker = registration.installing;
        worker?.addEventListener("statechange", () => {
            if (worker.state === "installed" && navigator.serviceWorker.controller) {
                setWaiting(worker);
            }
        });
    });

    setInterval(() => registration.update().catch(() => { }), updateCheckInterval);
};

if ("serviceWorker" in navigator) {
    navigator.serviceWorker.register("service-worker.js", { updateViaCache: "none" }).then(watch, () => { });
    navigator.serviceWorker.addEventListener("controllerchange", () => {
        if (updating) {
            updating = false;
            location.reload();
        }
    });
}

window.addEventListener("beforeinstallprompt", event => {
    event.preventDefault();
    installPrompt = event;
    notify();
});

window.addEventListener("appinstalled", () => {
    installPrompt = null;
    notify();
});


const isStandalone = () => matchMedia("(display-mode: standalone)").matches || navigator.standalone === true;

const isIos = () => /iPad|iPhone|iPod/.test(navigator.userAgent)
    || (navigator.platform === "MacIntel" && navigator.maxTouchPoints > 1);

const isMacSafari = () => /Macintosh/.test(navigator.userAgent) && navigator.maxTouchPoints === 0
    && /Version\/[\d.]+.*Safari/.test(navigator.userAgent) && !/Chrome|Chromium|Edg|Firefox/.test(navigator.userAgent);

const isAndroidFirefox = () => /Android/.test(navigator.userAgent) && /Firefox\//.test(navigator.userAgent);

const manualInstall = () => isIos() ? "ios" : isMacSafari() ? "mac" : isAndroidFirefox() ? "android-firefox" : null;

export function state() {
    const offered = !isStandalone() && !installDismissed;
    return {
        canInstall: offered && installPrompt !== null,
        manualInstall: offered && installPrompt === null ? manualInstall() : null,
        updateReady: waitingWorker !== null
    };
}

export function dismissInstall() {
    installDismissed = true;
    notify();
}

export async function install() {
    const prompt = installPrompt;
    if (prompt === null) {
        return false;
    }

    installPrompt = null;
    notify();
    await prompt.prompt();
    const choice = await prompt.userChoice;
    return choice.outcome === "accepted";
}

export function applyUpdate() {
    if (waitingWorker === null) {
        return;
    }

    updating = true;
    waitingWorker.postMessage("skip-waiting");
}

export function subscribe(target) {
    const listener = () => target.invokeMethodAsync("OnChanged");
    listeners.add(listener);
    return { dispose: () => listeners.delete(listener) };
}
