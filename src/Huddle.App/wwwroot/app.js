window.teamComposer = {
    attach(el, dotnetRef) {
        el.addEventListener("keydown", (e) => {
            if (e.key === "Enter" && !e.shiftKey) {
                e.preventDefault();
                dotnetRef.invokeMethodAsync("SendAsync", el.value);
                el.value = "";
            }
        });
    },
};

window.teamScroll = {
    toBottom(el) {
        el.scrollTop = el.scrollHeight;
    },
};
