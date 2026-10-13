const sample = () => new File([""], "plan.pdf", { type: "application/pdf" });

export function canShareFiles() {
    try {
        return typeof navigator.canShare === "function" && navigator.canShare({ files: [sample()] });
    } catch {
        return false;
    }
}

export async function shareFile(bytes, name, type, title, text) {
    return await share({ files: [new File([bytes], name, { type })], title, text });
}

export function download(bytes, name, type) {
    const url = URL.createObjectURL(new Blob([bytes], { type }));
    const link = document.createElement("a");
    link.href = url;
    link.download = name;
    document.body.appendChild(link);
    link.click();
    link.remove();
    setTimeout(() => URL.revokeObjectURL(url), 10000);
}

const isMobile = () => /Android|iPhone|iPad|iPod/.test(navigator.userAgent)
    || (navigator.platform === "MacIntel" && navigator.maxTouchPoints > 1);

export function printPdf(bytes) {
    const url = URL.createObjectURL(new Blob([bytes], { type: "application/pdf" }));
    const release = () => setTimeout(() => URL.revokeObjectURL(url), 60000);

    if (isMobile()) {
        window.open(url, "_blank");
        release();
        return "opened";
    }

    const frame = document.createElement("iframe");
    frame.style.cssText = "position:fixed;right:0;bottom:0;width:0;height:0;border:0";
    frame.addEventListener("load", () => {
        frame.contentWindow.focus();
        frame.contentWindow.print();
        setTimeout(() => frame.remove(), 60000);
        release();
    });
    frame.src = url;
    document.body.appendChild(frame);
    return "printing";
}

async function share(data) {
    try {
        await navigator.share(data);
        return "shared";
    } catch (error) {
        return error && error.name === "AbortError" ? "cancelled" : "failed";
    }
}
