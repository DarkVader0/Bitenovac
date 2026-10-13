export function track(svg, listener) {
    let frame = 0;
    let latest = null;

    const toChart = event => new DOMPoint(event.clientX, event.clientY).matrixTransform(svg.getScreenCTM().inverse()).x;

    const move = event => {
        latest = toChart(event);
        if (frame === 0) {
            frame = requestAnimationFrame(() => {
                frame = 0;
                listener.invokeMethodAsync("Point", latest);
            });
        }
    };

    // A finger lifting is not leaving: the readout stays until the next touch.
    const leave = event => {
        if (event.pointerType === "mouse") {
            listener.invokeMethodAsync("Leave");
        }
    };

    svg.addEventListener("pointerdown", move);
    svg.addEventListener("pointermove", move);
    svg.addEventListener("pointerleave", leave);

    return {
        dispose() {
            cancelAnimationFrame(frame);
            svg.removeEventListener("pointerdown", move);
            svg.removeEventListener("pointermove", move);
            svg.removeEventListener("pointerleave", leave);
        }
    };
}
