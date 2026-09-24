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

window.huddleStorage = {
  get: function (key) { try { return window.localStorage.getItem(key); } catch { return null; } },
  set: function (key, value) { try { window.localStorage.setItem(key, value); } catch { } }
};

window.huddleClipboard = {
  // Returns true only when the text really reached the clipboard.
  copy: async function (text) {
    if (window.isSecureContext && navigator.clipboard) {
      try { await navigator.clipboard.writeText(text); return true; } catch { /* fall through */ }
    }
    const area = document.createElement("textarea");
    area.value = text; area.setAttribute("readonly", ""); area.style.position = "fixed"; area.style.opacity = "0";
    document.body.appendChild(area); area.select();
    let ok = false;
    try { ok = document.execCommand("copy"); } catch { ok = false; }
    area.remove();
    return ok;
  }
};
