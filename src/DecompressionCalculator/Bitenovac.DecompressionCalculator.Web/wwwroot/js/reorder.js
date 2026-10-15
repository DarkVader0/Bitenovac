export function attach(list, dotnet) {
    const items = () => [...list.querySelectorAll(":scope > .block-item")];
    let drag = null;

    const clear = () => {
        for (const item of items()) {
            item.style.transform = "";
            item.classList.remove("dragging", "shifting");
        }
        drag = null;
    };

    const onDown = event => {
        const handle = event.target.closest(".block-handle");
        if (!handle || (event.pointerType === "mouse" && event.button !== 0)) {
            return;
        }

        const all = items();
        const item = handle.closest(".block-item");
        const from = all.indexOf(item);
        const rects = all.map(other => other.getBoundingClientRect());
        drag = {
            all, item, from, to: from, rects, handle,
            pointer: event.pointerId,
            startY: event.clientY,
            pitch: rects.length > 1 ? rects[1].top - rects[0].top : rects[0].height
        };
        handle.setPointerCapture(event.pointerId);
        item.classList.add("dragging");
        all.filter(other => other !== item).forEach(other => other.classList.add("shifting"));
        event.preventDefault();
    };

    const onMove = event => {
        if (!drag || event.pointerId !== drag.pointer) {
            return;
        }

        const { rects, from, pitch } = drag;
        const lowest = rects[rects.length - 1].top;
        const offset = Math.min(Math.max(event.clientY - drag.startY, rects[0].top - rects[from].top), lowest - rects[from].top);
        drag.item.style.transform = `translateY(${offset}px)`;
        drag.to = Math.min(Math.max(Math.round((rects[from].top + offset - rects[0].top) / pitch), 0), rects.length - 1);

        drag.all.forEach((other, index) => {
            if (other === drag.item) {
                return;
            }

            const shift = from < drag.to && index > from && index <= drag.to ? -pitch
                : from > drag.to && index >= drag.to && index < from ? pitch
                    : 0;
            other.style.transform = shift === 0 ? "" : `translateY(${shift}px)`;
        });
    };

    const onUp = event => {
        if (!drag || event.pointerId !== drag.pointer) {
            return;
        }

        const { from, to, handle } = drag;
        clear();
        if (event.type === "pointerup" && from !== to) {
            dotnet.invokeMethodAsync("Reorder", from, to).then(() => handle.isConnected && handle.focus());
        }
    };

    const onKey = event => {
        const handle = event.target.closest(".block-handle");
        const step = event.key === "ArrowUp" ? -1 : event.key === "ArrowDown" ? 1 : 0;
        if (!handle || step === 0) {
            return;
        }

        const all = items();
        const from = all.indexOf(handle.closest(".block-item"));
        const to = from + step;
        event.preventDefault();
        if (to >= 0 && to < all.length) {
            const block = handle.closest(".block-item").dataset.block;
            dotnet.invokeMethodAsync("Reorder", from, to)
                .then(() => list.querySelector(`[data-block="${block}"] .block-handle`)?.focus());
        }
    };

    list.addEventListener("pointerdown", onDown);
    list.addEventListener("pointermove", onMove);
    list.addEventListener("pointerup", onUp);
    list.addEventListener("pointercancel", onUp);
    list.addEventListener("keydown", onKey);

    return {
        dispose() {
            list.removeEventListener("pointerdown", onDown);
            list.removeEventListener("pointermove", onMove);
            list.removeEventListener("pointerup", onUp);
            list.removeEventListener("pointercancel", onUp);
            list.removeEventListener("keydown", onKey);
        }
    };
}
