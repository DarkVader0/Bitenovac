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

export function print() {
    window.print();
}

async function share(data) {
    try {
        await navigator.share(data);
        return "shared";
    } catch (error) {
        return error && error.name === "AbortError" ? "cancelled" : "failed";
    }
}
